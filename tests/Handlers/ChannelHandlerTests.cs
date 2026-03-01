using System;
using FluentAssertions;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Moq;
using WinTakMeshtasticPlugin.Handlers;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Handlers
{
    public class ChannelHandlerTests
    {
        private readonly ChannelHandler _handler;
        private readonly Mock<IChannelManager> _channelManagerMock;

        public ChannelHandlerTests()
        {
            _channelManagerMock = new Mock<IChannelManager>();
            _handler = new ChannelHandler(_channelManagerMock.Object);
        }

        [Fact]
        public void Constructor_NullChannelManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelHandler(null!));
        }

        [Fact]
        public void HandleChannel_ValidPrimaryChannel_UpdatesChannelManager()
        {
            // Arrange
            var channel = new Channel
            {
                Index = 0,
                Role = Channel.Types.Role.Primary,
                Settings = new ChannelSettings
                {
                    Name = "LongFast",
                    Psk = ByteString.CopyFrom(new byte[] { 0x01, 0x02, 0x03 })
                }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                0,
                "LongFast",
                ChannelRole.Primary,
                true // hasPsk
            ), Times.Once);
        }

        [Fact]
        public void HandleChannel_SecondaryChannel_UpdatesWithCorrectRole()
        {
            // Arrange
            var channel = new Channel
            {
                Index = 1,
                Role = Channel.Types.Role.Secondary,
                Settings = new ChannelSettings
                {
                    Name = "Team"
                }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                1,
                "Team",
                ChannelRole.Secondary,
                false // no PSK
            ), Times.Once);
        }

        [Fact]
        public void HandleChannel_DisabledChannel_UpdatesWithDisabledRole()
        {
            // Arrange
            var channel = new Channel
            {
                Index = 2,
                Role = Channel.Types.Role.Disabled,
                Settings = new ChannelSettings()
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                2,
                null,
                ChannelRole.Disabled,
                false
            ), Times.Once);
        }

        [Fact]
        public void HandleChannel_NullChannel_DoesNotThrow()
        {
            // Act & Assert - should not throw
            _handler.HandleChannel(null!);

            // Verify no update was called
            _channelManagerMock.Verify(
                m => m.UpdateChannel(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<ChannelRole>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public void HandleChannel_NullSettings_UpdatesWithNullName()
        {
            // Arrange
            var channel = new Channel
            {
                Index = 0,
                Role = Channel.Types.Role.Primary
                // Settings is null
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                0,
                null,
                ChannelRole.Primary,
                false // no PSK because no settings
            ), Times.Once);
        }

        [Fact]
        public void HandleChannel_EmptyPsk_ReportsNoPsk()
        {
            // Arrange - SEC-04: PSK presence only, not value
            var channel = new Channel
            {
                Index = 0,
                Role = Channel.Types.Role.Primary,
                Settings = new ChannelSettings
                {
                    Name = "Test",
                    Psk = ByteString.Empty
                }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                0,
                "Test",
                ChannelRole.Primary,
                false // Empty PSK = no PSK
            ), Times.Once);
        }

        [Fact]
        public void HandleChannel_AllChannelIndices_HandlesCorrectly()
        {
            // Arrange & Act - test all valid channel indices (0-7)
            for (int i = 0; i < 8; i++)
            {
                var channel = new Channel
                {
                    Index = i,
                    Role = i == 0 ? Channel.Types.Role.Primary : Channel.Types.Role.Secondary,
                    Settings = new ChannelSettings { Name = $"Channel {i}" }
                };

                _handler.HandleChannel(channel);
            }

            // Assert - all 8 channels should be updated
            _channelManagerMock.Verify(
                m => m.UpdateChannel(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<ChannelRole>(), It.IsAny<bool>()),
                Times.Exactly(8));
        }

        [Fact]
        public void HandleChannel_WithPsk_NeverStoresPskValue()
        {
            // Arrange - SEC-04: Never store or log PSK value
            var pskBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            var channel = new Channel
            {
                Index = 0,
                Role = Channel.Types.Role.Primary,
                Settings = new ChannelSettings
                {
                    Name = "Test",
                    Psk = ByteString.CopyFrom(pskBytes)
                }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert - only hasPsk bool is passed, not the actual value
            _channelManagerMock.Verify(m => m.UpdateChannel(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<ChannelRole>(),
                true), // hasPsk = true, but actual bytes are not passed
                Times.Once);

            // The ChannelHandler should not expose the PSK bytes in any way
            // This is enforced by the API: UpdateChannel only takes a bool for hasPsk
        }

        [Fact]
        public void HandleChannel_AdminChannelName_PassesNameToManager()
        {
            // Arrange - admin channel detection is handled by ChannelManager
            var channel = new Channel
            {
                Index = 0,
                Role = Channel.Types.Role.Primary,
                Settings = new ChannelSettings
                {
                    Name = "admin"
                }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert - handler passes name, ChannelManager handles admin detection
            _channelManagerMock.Verify(m => m.UpdateChannel(
                0,
                "admin",
                ChannelRole.Primary,
                false
            ), Times.Once);
        }

        [Theory]
        [InlineData(Channel.Types.Role.Primary, ChannelRole.Primary)]
        [InlineData(Channel.Types.Role.Secondary, ChannelRole.Secondary)]
        [InlineData(Channel.Types.Role.Disabled, ChannelRole.Disabled)]
        public void HandleChannel_RoleMapping_MapsCorrectly(Channel.Types.Role protoRole, ChannelRole expectedRole)
        {
            // Arrange
            var channel = new Channel
            {
                Index = 0,
                Role = protoRole,
                Settings = new ChannelSettings { Name = "Test" }
            };

            // Act
            _handler.HandleChannel(channel);

            // Assert
            _channelManagerMock.Verify(m => m.UpdateChannel(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                expectedRole,
                It.IsAny<bool>()
            ), Times.Once);
        }
    }
}
