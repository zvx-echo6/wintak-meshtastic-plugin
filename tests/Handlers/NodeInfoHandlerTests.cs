using System;
using System.Threading.Tasks;
using System.Xml;
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
    public class NodeInfoHandlerTests
    {
        private readonly NodeInfoHandler _handler;
        private readonly Mock<INodeStateManager> _nodeStateManagerMock;
        private readonly Mock<ICotBuilder> _cotBuilderMock;
        private readonly PacketHandlerContext _context;

        public NodeInfoHandlerTests()
        {
            _handler = new NodeInfoHandler();
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
        public void HandledPortNum_ShouldBeNodeinfoApp()
        {
            _handler.HandledPortNum.Should().Be(PortNum.NodeinfoApp);
        }

        [Fact]
        public async Task HandleAsync_ValidUser_UpdatesNodeState()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User
            {
                ShortName = "NODE",
                LongName = "Test Node Device",
                HwModel = HardwareModel.TloraV211P6,
                Role = Config.Types.DeviceConfig.Types.Role.Client
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.UpdatesNodeState.Should().BeTrue();

            nodeState.ShortName.Should().Be("NODE");
            nodeState.LongName.Should().Be("Test Node Device");
            nodeState.HardwareModel.Should().Be("TloraV211P6"); // Enum name from HardwareModel.TloraV211P6
            nodeState.Role.Should().Be(DeviceRole.Client);
        }

        [Fact]
        public async Task HandleAsync_RouterRole_MapsCorrectly()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User
            {
                ShortName = "RTR",
                Role = Config.Types.DeviceConfig.Types.Role.Router
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.Role.Should().Be(DeviceRole.Router);
        }

        [Fact]
        public async Task HandleAsync_TrackerRole_MapsCorrectly()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User
            {
                ShortName = "TRK",
                Role = Config.Types.DeviceConfig.Types.Role.Tracker
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.Role.Should().Be(DeviceRole.Tracker);
        }

        [Fact]
        public async Task HandleAsync_NodeWithPosition_RegeneratesCoT()
        {
            // Arrange - node already has position
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "OLD",
                Latitude = 42.75,
                Longitude = -114.46
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var expectedCotXml = "<?xml version=\"1.0\"?><event/>";
            _cotBuilderMock.Setup(m => m.BuildNodePli(It.IsAny<NodeState>(), It.IsAny<TimeSpan?>()))
                .Returns(expectedCotXml);

            var user = new User
            {
                ShortName = "NEW",  // Changed shortname
                LongName = "New Name"
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert - should regenerate CoT because shortname changed and node has position
            result.Should().NotBeNull();
            result!.CotXml.Should().Be(expectedCotXml);
            _cotBuilderMock.Verify(m => m.BuildNodePli(It.IsAny<NodeState>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_NodeWithoutPosition_NoCoTGenerated()
        {
            // Arrange - node has no position
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                Latitude = null,
                Longitude = null
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User
            {
                ShortName = "NODE",
                LongName = "Test Node"
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert - no CoT generated since no position
            result.Should().NotBeNull();
            result!.CotXml.Should().BeNull();
            result.UpdatesNodeState.Should().BeTrue();
        }

        [Fact]
        public async Task HandleAsync_SameShortName_NoCoTRegeneration()
        {
            // Arrange - node has position but shortname unchanged
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "SAME",
                Latitude = 42.75,
                Longitude = -114.46
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User
            {
                ShortName = "SAME",  // Same shortname
                LongName = "Updated Long Name"  // Only long name changed
            };

            var packet = CreatePacket(0x12345678, user);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert - no CoT regeneration needed
            result.Should().NotBeNull();
            result!.CotXml.Should().BeNull();
            _cotBuilderMock.Verify(m => m.BuildNodePli(It.IsAny<NodeState>(), It.IsAny<TimeSpan?>()), Times.Never);
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
                    Portnum = PortNum.NodeinfoApp,
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
                    Portnum = PortNum.NodeinfoApp,
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

            var user = new User { ShortName = "NODE" };
            var packet = CreatePacket(0x12345678, user);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutManager);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_SetsChannelMembership()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var user = new User { ShortName = "NODE" };
            var packet = CreatePacket(0x12345678, user, channel: 3);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.ChannelsMembership.Should().Contain(3);
        }

        [Fact]
        public async Task HandleAsync_AllRolesMapped()
        {
            // Test all role mappings
            var roles = new[]
            {
                (Config.Types.DeviceConfig.Types.Role.Client, DeviceRole.Client),
                (Config.Types.DeviceConfig.Types.Role.ClientMute, DeviceRole.ClientMute),
                (Config.Types.DeviceConfig.Types.Role.Router, DeviceRole.Router),
                (Config.Types.DeviceConfig.Types.Role.RouterClient, DeviceRole.RouterClient),
                (Config.Types.DeviceConfig.Types.Role.Repeater, DeviceRole.Repeater),
                (Config.Types.DeviceConfig.Types.Role.Tracker, DeviceRole.Tracker),
                (Config.Types.DeviceConfig.Types.Role.Sensor, DeviceRole.Sensor),
                (Config.Types.DeviceConfig.Types.Role.Tak, DeviceRole.Tak),
                (Config.Types.DeviceConfig.Types.Role.ClientHidden, DeviceRole.ClientHidden),
                (Config.Types.DeviceConfig.Types.Role.LostAndFound, DeviceRole.LostAndFound),
                (Config.Types.DeviceConfig.Types.Role.TakTracker, DeviceRole.TakTracker),
            };

            foreach (var (protoRole, expectedRole) in roles)
            {
                var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
                _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

                var user = new User { ShortName = "TEST", Role = protoRole };
                var packet = CreatePacket(0x12345678, user);

                await _handler.HandleAsync(packet, _context);

                nodeState.Role.Should().Be(expectedRole, $"Role {protoRole} should map to {expectedRole}");
            }
        }

        private static MeshPacket CreatePacket(uint fromNodeId, User user, uint channel = 0)
        {
            return new MeshPacket
            {
                From = fromNodeId,
                Channel = channel,
                Decoded = new Data
                {
                    Portnum = PortNum.NodeinfoApp,
                    Payload = user.ToByteString()
                }
            };
        }
    }
}
