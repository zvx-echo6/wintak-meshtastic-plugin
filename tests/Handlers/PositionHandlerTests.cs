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
    public class PositionHandlerTests
    {
        private readonly PositionHandler _handler;
        private readonly Mock<INodeStateManager> _nodeStateManagerMock;
        private readonly Mock<ICotBuilder> _cotBuilderMock;
        private readonly PacketHandlerContext _context;

        public PositionHandlerTests()
        {
            _handler = new PositionHandler();
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
        public void HandledPortNum_ShouldBePositionApp()
        {
            _handler.HandledPortNum.Should().Be(PortNum.PositionApp);
        }

        [Fact]
        public async Task HandleAsync_ValidPosition_GeneratesCorrectCoT()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var expectedCotXml = "<?xml version=\"1.0\"?><event/>";
            _cotBuilderMock.Setup(m => m.BuildNodePli(It.IsAny<NodeState>(), It.IsAny<TimeSpan?>()))
                .Returns(expectedCotXml);

            // Create a Position with lat=42.75, lon=-114.46 (scaled by 1e7)
            var position = new Position
            {
                LatitudeI = 427500000,   // 42.75
                LongitudeI = -1144600000, // -114.46
                Altitude = 1200
            };

            var packet = CreatePacket(0x12345678, position);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.CotXml.Should().Be(expectedCotXml);
            result.UpdatesNodeState.Should().BeTrue();

            // Verify node state was updated
            nodeState.Latitude.Should().BeApproximately(42.75, 0.0001);
            nodeState.Longitude.Should().BeApproximately(-114.46, 0.0001);
            nodeState.Altitude.Should().Be(1200);
        }

        [Fact]
        public async Task HandleAsync_ValidPosition_SetsCorrectChannelMembership()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _cotBuilderMock.Setup(m => m.BuildNodePli(It.IsAny<NodeState>(), It.IsAny<TimeSpan?>()))
                .Returns("<event/>");

            var position = new Position { LatitudeI = 400000000, LongitudeI = -1000000000 };
            var packet = CreatePacket(0x12345678, position, channel: 2);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.ChannelsMembership.Should().Contain(2);
        }

        [Fact]
        public async Task HandleAsync_ZeroCoordinates_ReturnsNull()
        {
            // Arrange
            var position = new Position { LatitudeI = 0, LongitudeI = 0 };
            var packet = CreatePacket(0x12345678, position);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_InvalidCoordinates_ReturnsNull()
        {
            // Arrange - latitude > 90 degrees
            var position = new Position { LatitudeI = 1000000000, LongitudeI = 0 };
            var packet = CreatePacket(0x12345678, position);

            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
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
                    Portnum = PortNum.PositionApp,
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
                    Portnum = PortNum.PositionApp,
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

            var position = new Position { LatitudeI = 400000000, LongitudeI = -1000000000 };
            var packet = CreatePacket(0x12345678, position);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutManager);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_NoCotBuilder_ReturnsNull()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var contextWithoutBuilder = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                CotBuilder = null
            };

            var position = new Position { LatitudeI = 400000000, LongitudeI = -1000000000 };
            var packet = CreatePacket(0x12345678, position);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutBuilder);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_ValidPosition_CoTContainsRequiredElements()
        {
            // Arrange - use real CotBuilder to verify XML structure
            var realCotBuilder = new CotBuilder();
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "TEST"
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var context = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                CotBuilder = realCotBuilder
            };

            var position = new Position
            {
                LatitudeI = 427500000,
                LongitudeI = -1144600000,
                Altitude = 1200
            };
            var packet = CreatePacket(0x12345678, position);

            // Act
            var result = await _handler.HandleAsync(packet, context);

            // Assert - parse and verify CoT XML
            result.Should().NotBeNull();
            result!.CotXml.Should().NotBeNullOrEmpty();

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(result.CotXml);

            var eventNode = xmlDoc.SelectSingleNode("/event");
            eventNode.Should().NotBeNull();
            eventNode!.Attributes!["type"]!.Value.Should().Be("a-f-G-U-U-S-R"); // Client = Mode 1 (Radio unit type, __group provides visual)
            eventNode.Attributes["uid"]!.Value.Should().Contain("MESH-12345678"); // Stable UID without connectionId

            var pointNode = xmlDoc.SelectSingleNode("/event/point");
            pointNode.Should().NotBeNull();
            pointNode!.Attributes!["lat"]!.Value.Should().Contain("42.75");
            pointNode.Attributes["lon"]!.Value.Should().Contain("-114.46");

            var contactNode = xmlDoc.SelectSingleNode("/event/detail/contact");
            contactNode.Should().NotBeNull();
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("TEST");

            // Mode 1 (Client) has __group for channel-colored circle
            var groupNode = xmlDoc.SelectSingleNode("/event/detail/__group");
            groupNode.Should().NotBeNull();
            groupNode!.Attributes!["name"]!.Value.Should().Be("Dark Blue"); // Channel 0
        }

        private static MeshPacket CreatePacket(uint fromNodeId, Position position, uint channel = 0)
        {
            return new MeshPacket
            {
                From = fromNodeId,
                Channel = channel,
                Decoded = new Data
                {
                    Portnum = PortNum.PositionApp,
                    Payload = position.ToByteString()
                }
            };
        }
    }
}
