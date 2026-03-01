using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Meshtastic.Protobufs;
using Moq;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.Messaging;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Messaging
{
    public class OutboundMessageServiceTests
    {
        private readonly Mock<IMeshtasticClient> _clientMock;
        private readonly Mock<IChannelManager> _channelManagerMock;
        private readonly OutboundMessageService _service;

        public OutboundMessageServiceTests()
        {
            _clientMock = new Mock<IMeshtasticClient>();
            _channelManagerMock = new Mock<IChannelManager>();

            _clientMock.SetupGet(c => c.State).Returns(ConnectionState.Connected);
            _channelManagerMock.SetupGet(c => c.SelectedOutboundChannel).Returns(0);

            _service = new OutboundMessageService(_clientMock.Object, _channelManagerMock.Object);
        }

        #region SendTextMessageAsync Tests

        [Fact]
        public async Task SendTextMessageAsync_ValidMessage_SendsPacket()
        {
            // Arrange
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            MeshPacket? sentPacket = null;
            _clientMock.Setup(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()))
                .Callback<MeshPacket, CancellationToken>((p, ct) => sentPacket = p)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SendTextMessageAsync("Hello mesh!");

            // Assert
            result.Should().BeTrue();
            sentPacket.Should().NotBeNull();
            sentPacket!.Decoded.Portnum.Should().Be(PortNum.TextMessageApp);
            sentPacket.Channel.Should().Be(0);
            sentPacket.To.Should().Be(0xFFFFFFFF); // Broadcast
            Encoding.UTF8.GetString(sentPacket.Decoded.Payload.ToByteArray()).Should().Be("Hello mesh!");
        }

        [Fact]
        public async Task SendTextMessageAsync_ValidMessage_RaisesMessageSentEvent()
        {
            // Arrange
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            OutboundMessageSentEventArgs? eventArgs = null;
            _service.MessageSent += (sender, args) => eventArgs = args;

            // Act
            await _service.SendTextMessageAsync("Test message");

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs!.Message.Should().Be("Test message");
            eventArgs.ChannelIndex.Should().Be(0);
            eventArgs.ChannelName.Should().Be("Primary");
        }

        [Fact]
        public async Task SendTextMessageAsync_EmptyMessage_ReturnsFalse()
        {
            // Act
            var result = await _service.SendTextMessageAsync("");

            // Assert
            result.Should().BeFalse();
            _clientMock.Verify(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendTextMessageAsync_NullMessage_ReturnsFalse()
        {
            // Act
            var result = await _service.SendTextMessageAsync(null!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendTextMessageAsync_NotConnected_ReturnsFalse()
        {
            // Arrange
            _clientMock.SetupGet(c => c.State).Returns(ConnectionState.Disconnected);

            // Act
            var result = await _service.SendTextMessageAsync("Test");

            // Assert
            result.Should().BeFalse();
            _clientMock.Verify(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Message Length Tests (MSG-03)

        [Fact]
        public async Task SendTextMessageAsync_ExactlyMaxLength_Succeeds()
        {
            // Arrange - MSG-03: Max message length is 228 bytes
            var message = new string('A', 228);
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            // Act
            var result = await _service.SendTextMessageAsync(message);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendTextMessageAsync_ExceedsMaxLength_ReturnsFalse()
        {
            // Arrange - MSG-03: Messages > 228 bytes should fail
            var message = new string('A', 229);
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            // Act
            var result = await _service.SendTextMessageAsync(message);

            // Assert
            result.Should().BeFalse();
            _clientMock.Verify(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendTextMessageAsync_UnicodeExceedsMaxBytes_ReturnsFalse()
        {
            // Arrange - Unicode characters can be multiple bytes
            // 76 Chinese characters = 228 bytes (3 bytes each)
            var message = new string('\u4e16', 76); // exactly 228 bytes
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            var result1 = await _service.SendTextMessageAsync(message);
            result1.Should().BeTrue();

            // 77 Chinese characters = 231 bytes (exceeds limit)
            var message2 = new string('\u4e16', 77);
            var result2 = await _service.SendTextMessageAsync(message2);
            result2.Should().BeFalse();
        }

        [Fact]
        public void GetRemainingBytes_EmptyMessage_ReturnsMax()
        {
            // Act
            var remaining = _service.GetRemainingBytes("");

            // Assert
            remaining.Should().Be(228);
        }

        [Fact]
        public void GetRemainingBytes_NullMessage_ReturnsMax()
        {
            // Act
            var remaining = _service.GetRemainingBytes(null!);

            // Assert
            remaining.Should().Be(228);
        }

        [Fact]
        public void GetRemainingBytes_PartialMessage_ReturnsCorrectCount()
        {
            // Act
            var remaining = _service.GetRemainingBytes("Hello"); // 5 bytes

            // Assert
            remaining.Should().Be(223);
        }

        [Fact]
        public void GetRemainingBytes_ExactMax_ReturnsZero()
        {
            // Act
            var remaining = _service.GetRemainingBytes(new string('A', 228));

            // Assert
            remaining.Should().Be(0);
        }

        [Fact]
        public void GetRemainingBytes_ExceedsMax_ReturnsZero()
        {
            // Act
            var remaining = _service.GetRemainingBytes(new string('A', 300));

            // Assert
            remaining.Should().Be(0);
        }

        #endregion

        #region SendTextMessageToChannelAsync Tests

        [Fact]
        public async Task SendTextMessageToChannelAsync_ValidChannel_SendsToSpecificChannel()
        {
            // Arrange
            var channel = new ChannelState { Index = 2, TransmitEnabled = true, Name = "Team" };
            _channelManagerMock.Setup(m => m.GetChannel(2)).Returns(channel);

            MeshPacket? sentPacket = null;
            _clientMock.Setup(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()))
                .Callback<MeshPacket, CancellationToken>((p, ct) => sentPacket = p)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SendTextMessageToChannelAsync("Test", 2);

            // Assert
            result.Should().BeTrue();
            sentPacket.Should().NotBeNull();
            sentPacket!.Channel.Should().Be(2);
        }

        [Fact]
        public async Task SendTextMessageToChannelAsync_TransmitDisabled_ReturnsFalse()
        {
            // Arrange
            var channel = new ChannelState { Index = 1, TransmitEnabled = false, Name = "Admin" };
            _channelManagerMock.Setup(m => m.GetChannel(1)).Returns(channel);

            // Act
            var result = await _service.SendTextMessageToChannelAsync("Test", 1);

            // Assert
            result.Should().BeFalse();
            _clientMock.Verify(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendTextMessageToChannelAsync_UnknownChannel_ReturnsFalse()
        {
            // Arrange
            _channelManagerMock.Setup(m => m.GetChannel(99)).Returns((ChannelState?)null);

            // Act
            var result = await _service.SendTextMessageToChannelAsync("Test", 99);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region SendDirectMessageAsync Tests

        [Fact]
        public async Task SendDirectMessageAsync_ValidDestination_SendsWithWantAck()
        {
            // Arrange
            MeshPacket? sentPacket = null;
            _clientMock.Setup(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()))
                .Callback<MeshPacket, CancellationToken>((p, ct) => sentPacket = p)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SendDirectMessageAsync("DM test", 0x12345678, 0);

            // Assert
            result.Should().BeTrue();
            sentPacket.Should().NotBeNull();
            sentPacket!.To.Should().Be(0x12345678);
            sentPacket.WantAck.Should().BeTrue();
            sentPacket.Decoded.Portnum.Should().Be(PortNum.TextMessageApp);
        }

        [Fact]
        public async Task SendDirectMessageAsync_EmptyMessage_ReturnsFalse()
        {
            // Act
            var result = await _service.SendDirectMessageAsync("", 0x12345678, 0);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendDirectMessageAsync_NotConnected_ReturnsFalse()
        {
            // Arrange
            _clientMock.SetupGet(c => c.State).Returns(ConnectionState.Disconnected);

            // Act
            var result = await _service.SendDirectMessageAsync("Test", 0x12345678, 0);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendDirectMessageAsync_ExceedsMaxLength_ReturnsFalse()
        {
            // Arrange
            var message = new string('A', 229);

            // Act
            var result = await _service.SendDirectMessageAsync(message, 0x12345678, 0);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task SendTextMessageAsync_ClientThrows_ReturnsFalse()
        {
            // Arrange
            var channel = new ChannelState { Index = 0, TransmitEnabled = true, Name = "Primary" };
            _channelManagerMock.Setup(m => m.GetChannel(0)).Returns(channel);

            _clientMock.Setup(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            // Act
            var result = await _service.SendTextMessageAsync("Test");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendDirectMessageAsync_ClientThrows_ReturnsFalse()
        {
            // Arrange
            _clientMock.Setup(c => c.SendPacketAsync(It.IsAny<MeshPacket>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            // Act
            var result = await _service.SendDirectMessageAsync("Test", 0x12345678, 0);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new OutboundMessageService(null!, _channelManagerMock.Object));
        }

        [Fact]
        public void Constructor_NullChannelManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new OutboundMessageService(_clientMock.Object, null!));
        }

        #endregion

        #region MaxMessageLength Constant Test

        [Fact]
        public void MaxMessageLength_Is228()
        {
            // Assert - MSG-03 compliance
            OutboundMessageService.MaxMessageLength.Should().Be(228);
        }

        #endregion
    }
}
