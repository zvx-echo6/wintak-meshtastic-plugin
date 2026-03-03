using System;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Moq;
using WinTakMeshtasticPlugin.CoT;
using WinTakMeshtasticPlugin.Handlers;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Handlers
{
    public class TelemetryHandlerTests
    {
        private readonly TelemetryHandler _handler;
        private readonly Mock<INodeStateManager> _nodeStateManagerMock;
        private readonly Mock<ICotBuilder> _cotBuilderMock;
        private readonly PacketHandlerContext _context;

        public TelemetryHandlerTests()
        {
            _handler = new TelemetryHandler();
            _nodeStateManagerMock = new Mock<INodeStateManager>();
            _cotBuilderMock = new Mock<ICotBuilder>();

            _context = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                CotBuilder = _cotBuilderMock.Object
            };
        }

        [Fact]
        public void HandledPortNum_ShouldBeTelemetryApp()
        {
            _handler.HandledPortNum.Should().Be(PortNum.TelemetryApp);
        }

        [Fact]
        public async Task HandleAsync_DeviceMetrics_UpdatesNodeState()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var deviceMetrics = new DeviceMetrics
            {
                BatteryLevel = 85,
                Voltage = 4.1f,
                ChannelUtilization = 12.5f,
                AirUtilTx = 3.2f,
                UptimeSeconds = 86400  // 1 day
            };

            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.CotXml.Should().BeNull(); // Telemetry doesn't generate CoT
            result.UpdatesNodeState.Should().BeTrue();

            nodeState.DeviceTelemetry.Should().NotBeNull();
            nodeState.DeviceTelemetry!.BatteryLevel.Should().Be(85);
            nodeState.DeviceTelemetry.Voltage.Should().BeApproximately(4.1f, 0.01f);
            nodeState.DeviceTelemetry.ChannelUtilization.Should().BeApproximately(12.5f, 0.1f);
            nodeState.DeviceTelemetry.AirUtilTx.Should().BeApproximately(3.2f, 0.1f);
            nodeState.DeviceTelemetry.UptimeSeconds.Should().Be(86400u);
        }

        [Fact]
        public async Task HandleAsync_EnvironmentMetrics_UpdatesNodeState()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var envMetrics = new EnvironmentMetrics
            {
                Temperature = 22.5f,
                RelativeHumidity = 45.0f,
                BarometricPressure = 1013.25f,
                GasResistance = 50000f,
                Iaq = 75
            };

            var telemetry = new Telemetry { EnvironmentMetrics = envMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.UpdatesNodeState.Should().BeTrue();

            nodeState.EnvironmentTelemetry.Should().NotBeNull();
            nodeState.EnvironmentTelemetry!.Temperature.Should().BeApproximately(22.5f, 0.1f);
            nodeState.EnvironmentTelemetry.RelativeHumidity.Should().BeApproximately(45.0f, 0.1f);
            nodeState.EnvironmentTelemetry.BarometricPressure.Should().BeApproximately(1013.25f, 0.1f);
            nodeState.EnvironmentTelemetry.GasResistance.Should().BeApproximately(50000f, 1f);
            nodeState.EnvironmentTelemetry.Iaq.Should().BeApproximately(75f, 0.1f);
        }

        [Fact]
        public async Task HandleAsync_PowerMetrics_UpdatesVoltage()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var powerMetrics = new PowerMetrics
            {
                Ch1Voltage = 12.6f
            };

            var telemetry = new Telemetry { PowerMetrics = powerMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.UpdatesNodeState.Should().BeTrue();

            nodeState.DeviceTelemetry.Should().NotBeNull();
            nodeState.DeviceTelemetry!.Voltage.Should().BeApproximately(12.6f, 0.1f);
        }

        [Fact]
        public async Task HandleAsync_PartialDeviceMetrics_StoresAvailableFields()
        {
            // Arrange - only battery level, no voltage or uptime
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var deviceMetrics = new DeviceMetrics
            {
                BatteryLevel = 50
            };

            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            nodeState.DeviceTelemetry.Should().NotBeNull();
            nodeState.DeviceTelemetry!.BatteryLevel.Should().Be(50);
            nodeState.DeviceTelemetry.Voltage.Should().BeNull();
            nodeState.DeviceTelemetry.UptimeSeconds.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_EmptyPayload_ReturnsNull()
        {
            // Arrange
            var packet = new MeshPacket
            {
                From = 0x12345678,
                Decoded = new Data
                {
                    Portnum = PortNum.TelemetryApp,
                    Payload = ByteString.Empty
                }
            };

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_MalformedProtobuf_ReturnsNull()
        {
            // Arrange
            var packet = new MeshPacket
            {
                From = 0x12345678,
                Decoded = new Data
                {
                    Portnum = PortNum.TelemetryApp,
                    Payload = ByteString.CopyFrom(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })
                }
            };

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert - should not throw, just return null
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_NoNodeStateManager_ReturnsNull()
        {
            // Arrange
            var contextWithoutManager = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = null,
                CotBuilder = _cotBuilderMock.Object
            };

            var deviceMetrics = new DeviceMetrics { BatteryLevel = 85 };
            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutManager);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_UnknownTelemetryVariant_ReturnsNull()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            // Empty telemetry (no variant set)
            var telemetry = new Telemetry();
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_UpdatesLastHeard()
        {
            // Arrange
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                LastHeard = DateTime.MinValue
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var deviceMetrics = new DeviceMetrics { BatteryLevel = 85 };
            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            var beforeCall = DateTime.UtcNow;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.LastHeard.Should().BeAfter(beforeCall.AddSeconds(-1));
            nodeState.LastHeard.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        }

        [Fact]
        public async Task HandleAsync_DeviceMetrics_FormatsUptimeCorrectly()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            // 2 days, 3 hours, 45 minutes = (2*24*60*60) + (3*60*60) + (45*60) = 186300 seconds
            var deviceMetrics = new DeviceMetrics
            {
                UptimeSeconds = 186300
            };

            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.DeviceTelemetry.Should().NotBeNull();
            nodeState.DeviceTelemetry!.UptimeFormatted.Should().Be("2d 3h 45m");
        }

        [Fact]
        public async Task HandleAsync_EnvironmentMetrics_CalculatesConversions()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var envMetrics = new EnvironmentMetrics
            {
                Temperature = 20.0f,  // 20°C = 68°F
                BarometricPressure = 1013.25f  // ~29.92 inHg
            };

            var telemetry = new Telemetry { EnvironmentMetrics = envMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.EnvironmentTelemetry.Should().NotBeNull();
            nodeState.EnvironmentTelemetry!.TemperatureFahrenheit.Should().BeApproximately(68.0f, 0.1f);
            nodeState.EnvironmentTelemetry.PressureInHg.Should().BeApproximately(29.92f, 0.1f);
        }

        [Fact]
        public async Task HandleAsync_AirQualityMetrics_UpdatesTimestamp()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var airQuality = new AirQualityMetrics
            {
                Pm10Standard = 10,
                Pm25Standard = 25
            };

            var telemetry = new Telemetry { AirQualityMetrics = airQuality };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.UpdatesNodeState.Should().BeTrue();
            nodeState.EnvironmentTelemetry.Should().NotBeNull();
        }

        [Fact]
        public async Task HandleAsync_CallsNodeStateManagerUpdate()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var deviceMetrics = new DeviceMetrics { BatteryLevel = 85 };
            var telemetry = new Telemetry { DeviceMetrics = deviceMetrics };
            var packet = CreatePacket(0x12345678, telemetry);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            _nodeStateManagerMock.Verify(m => m.Update(nodeState), Times.Once);
        }

        private static MeshPacket CreatePacket(uint fromNodeId, Telemetry telemetry)
        {
            return new MeshPacket
            {
                From = fromNodeId,
                Decoded = new Data
                {
                    Portnum = PortNum.TelemetryApp,
                    Payload = telemetry.ToByteString()
                }
            };
        }
    }
}
