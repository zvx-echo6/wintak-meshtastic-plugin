using System;
using System.Text;
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
    public class TextMessageHandlerTests
    {
        private readonly TextMessageHandler _handler;
        private readonly Mock<INodeStateManager> _nodeStateManagerMock;
        private readonly Mock<IChannelManager> _channelManagerMock;
        private readonly Mock<ICotBuilder> _cotBuilderMock;
        private readonly PacketHandlerContext _context;

        public TextMessageHandlerTests()
        {
            _handler = new TextMessageHandler();
            _nodeStateManagerMock = new Mock<INodeStateManager>();
            _channelManagerMock = new Mock<IChannelManager>();
            _cotBuilderMock = new Mock<ICotBuilder>();

            _context = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                ChannelManager = _channelManagerMock.Object,
                CotBuilder = _cotBuilderMock.Object
            };
        }

        [Fact]
        public void HandledPortNum_ShouldBeTextMessageApp()
        {
            _handler.HandledPortNum.Should().Be(PortNum.TextMessageApp);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_GeneratesGeoChat()
        {
            // Arrange
            var message = "Hello mesh!";
            var packet = CreateTextMessagePacket(0x12345678, message, channel: 0);

            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "ABCD"
            };
            _nodeStateManagerMock.Setup(m => m.Get("TEST", 0x12345678u)).Returns(nodeState);

            var channelState = new ChannelState { Index = 0, Name = "LongFast" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channelState);

            var expectedCotXml = "<?xml version=\"1.0\"?><event/>";
            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s == message),
                It.Is<string>(s => s.Contains("LongFast"))))
                .Returns(expectedCotXml);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.CotXml.Should().Be(expectedCotXml);
            result.UpdatesNodeState.Should().BeFalse();
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_RaisesMessageReceivedEvent()
        {
            // Arrange
            var message = "Test message";
            var packet = CreateTextMessagePacket(0x12345678, message, channel: 2);

            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "TST1"
            };
            _nodeStateManagerMock.Setup(m => m.Get("TEST", 0x12345678u)).Returns(nodeState);

            var channelState = new ChannelState { Index = 2, Name = "Team" };
            _channelManagerMock.Setup(m => m.GetChannel(2)).Returns(channelState);

            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .Returns("<event/>");

            TextMessageReceivedEventArgs? receivedArgs = null;
            _handler.MessageReceived += (sender, args) => receivedArgs = args;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.SenderNodeId.Should().Be(0x12345678u);
            receivedArgs.SenderCallsign.Should().Be("TST1");
            receivedArgs.ChannelIndex.Should().Be(2);
            receivedArgs.Message.Should().Be(message);
            receivedArgs.ChatRoom.Should().Contain("Team");
        }

        [Fact]
        public async Task HandleAsync_UnknownSender_UsesFallbackCallsign()
        {
            // Arrange
            var message = "Hello from unknown";
            var packet = CreateTextMessagePacket(0xABCDEF12, message, channel: 0);

            // No node state exists for this sender
            _nodeStateManagerMock.Setup(m => m.Get("TEST", 0xABCDEF12u)).Returns((NodeState?)null);

            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.Is<string>(s => s == "!abcdef12"), // Hex fallback
                It.IsAny<string>(),
                It.IsAny<string>()))
                .Returns("<event/>");

            TextMessageReceivedEventArgs? receivedArgs = null;
            _handler.MessageReceived += (sender, args) => receivedArgs = args;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.SenderCallsign.Should().Be("!abcdef12");
        }

        [Fact]
        public async Task HandleAsync_UnknownChannel_UsesDefaultChatRoomName()
        {
            // Arrange
            var message = "Test";
            var packet = CreateTextMessagePacket(0x12345678, message, channel: 5);

            // No channel state exists
            _channelManagerMock.Setup(m => m.GetChannel(5)).Returns((ChannelState?)null);

            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s == "Mesh: Channel 5")))
                .Returns("<event/>");

            TextMessageReceivedEventArgs? receivedArgs = null;
            _handler.MessageReceived += (sender, args) => receivedArgs = args;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.ChatRoom.Should().Be("Mesh: Channel 5");
        }

        [Fact]
        public async Task HandleAsync_DifferentChannels_GenerateDifferentChatRooms()
        {
            // Arrange - per MSG-02, different channels must use different chat windows
            var channel0 = new ChannelState { Index = 0, Name = "Primary" };
            var channel1 = new ChannelState { Index = 1, Name = "Secondary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel0);
            _channelManagerMock.Setup(m => m.GetChannel(1)).Returns(channel1);

            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .Returns("<event/>");

            var chatRooms = new System.Collections.Generic.List<string>();
            _handler.MessageReceived += (sender, args) => chatRooms.Add(args.ChatRoom);

            // Act
            var packet0 = CreateTextMessagePacket(0x12345678, "Msg1", channel: 0);
            var packet1 = CreateTextMessagePacket(0x12345678, "Msg2", channel: 1);

            await _handler.HandleAsync(packet0, _context);
            await _handler.HandleAsync(packet1, _context);

            // Assert - MSG-02 compliance
            chatRooms.Should().HaveCount(2);
            chatRooms[0].Should().NotBe(chatRooms[1]);
            chatRooms[0].Should().Contain("Primary");
            chatRooms[1].Should().Contain("Secondary");
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
                    Portnum = PortNum.TextMessageApp,
                    Payload = ByteString.Empty
                }
            };

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_EmptyMessage_ReturnsNull()
        {
            // Arrange
            var packet = CreateTextMessagePacket(0x12345678, "", channel: 0);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_NoCotBuilder_ReturnsResultWithNoXml()
        {
            // Arrange
            var contextWithoutBuilder = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                ChannelManager = _channelManagerMock.Object,
                CotBuilder = null
            };

            var packet = CreateTextMessagePacket(0x12345678, "Test", channel: 0);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutBuilder);

            // Assert
            result.Should().NotBeNull();
            result!.CotXml.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_UnicodeMessage_HandlesCorrectly()
        {
            // Arrange
            var message = "Hello \u4e16\u754c!"; // Hello World in Chinese
            var packet = CreateTextMessagePacket(0x12345678, message, channel: 0);

            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s == message),
                It.IsAny<string>()))
                .Returns("<event/>");

            TextMessageReceivedEventArgs? receivedArgs = null;
            _handler.MessageReceived += (sender, args) => receivedArgs = args;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Message.Should().Be(message);
        }

        [Fact]
        public async Task HandleAsync_GeneratesCorrectSenderUid()
        {
            // Arrange
            var message = "Test";
            var packet = CreateTextMessagePacket(0xABCDEF12, message, channel: 0);

            // UID is now stable without connectionId to prevent duplicate markers on reconnect
            _cotBuilderMock.Setup(m => m.BuildGeoChat(
                It.Is<string>(uid => uid == "MESH-ABCDEF12"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .Returns("<event/>");

            TextMessageReceivedEventArgs? receivedArgs = null;
            _handler.MessageReceived += (sender, args) => receivedArgs = args;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.SenderUid.Should().Be("MESH-ABCDEF12");
        }

        private static MeshPacket CreateTextMessagePacket(uint fromNodeId, string message, uint channel = 0)
        {
            return new MeshPacket
            {
                From = fromNodeId,
                Channel = channel,
                Decoded = new Data
                {
                    Portnum = PortNum.TextMessageApp,
                    Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(message))
                }
            };
        }
    }
}
