using System;
using System.Linq;
using FluentAssertions;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Models
{
    public class ChannelManagerTests
    {
        private readonly ChannelManager _manager;

        public ChannelManagerTests()
        {
            _manager = new ChannelManager();
        }

        #region UpdateChannel Tests

        [Fact]
        public void UpdateChannel_NewChannel_CreatesChannelState()
        {
            // Act
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);

            // Assert
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.Index.Should().Be(0);
            channel.Name.Should().Be("Primary");
            channel.Role.Should().Be(ChannelRole.Primary);
            channel.HasPsk.Should().BeTrue();
        }

        [Fact]
        public void UpdateChannel_ExistingChannel_UpdatesState()
        {
            // Arrange
            _manager.UpdateChannel(0, "Original", ChannelRole.Primary, hasPsk: false);

            // Act
            _manager.UpdateChannel(0, "Updated", ChannelRole.Secondary, hasPsk: true);

            // Assert
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.Name.Should().Be("Updated");
            channel.Role.Should().Be(ChannelRole.Secondary);
            channel.HasPsk.Should().BeTrue();
        }

        [Fact]
        public void UpdateChannel_SetsDefaultTeamColor()
        {
            // Act
            _manager.UpdateChannel(2, "Test", ChannelRole.Secondary, hasPsk: false);

            // Assert - Channel 2 should be Yellow
            var channel = _manager.GetChannel(2);
            channel.Should().NotBeNull();
            channel!.TeamColor.Should().Be("Yellow");
        }

        [Fact]
        public void UpdateChannel_RaisesChannelChangedEvent()
        {
            // Arrange
            ChannelChangedEventArgs? eventArgs = null;
            _manager.ChannelChanged += (sender, args) => eventArgs = args;

            // Act
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: false);

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs!.Channel.Index.Should().Be(0);
            eventArgs.Channel.Name.Should().Be("Test");
        }

        #endregion

        #region Admin Channel Detection Tests (SEC-02)

        [Fact]
        public void UpdateChannel_AdminInName_SetsIsAdminTrue()
        {
            // Act
            _manager.UpdateChannel(0, "admin", ChannelRole.Primary, hasPsk: true);

            // Assert
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.IsAdmin.Should().BeTrue();
        }

        [Fact]
        public void UpdateChannel_AdminInName_CaseInsensitive()
        {
            // Arrange & Act
            _manager.UpdateChannel(0, "ADMIN", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Admin Channel", ChannelRole.Secondary, hasPsk: true);
            _manager.UpdateChannel(2, "my-admin-chan", ChannelRole.Secondary, hasPsk: true);

            // Assert
            _manager.GetChannel(0)!.IsAdmin.Should().BeTrue();
            _manager.GetChannel(1)!.IsAdmin.Should().BeTrue();
            _manager.GetChannel(2)!.IsAdmin.Should().BeTrue();
        }

        [Fact]
        public void UpdateChannel_AdminChannel_DisablesTransmit()
        {
            // Act
            _manager.UpdateChannel(0, "admin", ChannelRole.Primary, hasPsk: true);

            // Assert - SEC-02: Admin channel excluded from transmit by default
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.TransmitEnabled.Should().BeFalse();
        }

        [Fact]
        public void UpdateChannel_NonAdminChannel_EnablesTransmit()
        {
            // Act
            _manager.UpdateChannel(0, "LongFast", ChannelRole.Primary, hasPsk: true);

            // Assert
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.IsAdmin.Should().BeFalse();
            channel.TransmitEnabled.Should().BeTrue();
        }

        #endregion

        #region Disabled Channel Tests

        [Fact]
        public void UpdateChannel_DisabledRole_DisablesTransmitAndReceive()
        {
            // Act
            _manager.UpdateChannel(0, "Disabled", ChannelRole.Disabled, hasPsk: false);

            // Assert
            var channel = _manager.GetChannel(0);
            channel.Should().NotBeNull();
            channel!.TransmitEnabled.Should().BeFalse();
            channel.ReceiveEnabled.Should().BeFalse();
        }

        #endregion

        #region GetTransmitChannels Tests

        [Fact]
        public void GetTransmitChannels_ExcludesDisabledChannels()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Secondary", ChannelRole.Secondary, hasPsk: true);
            _manager.UpdateChannel(2, "Disabled", ChannelRole.Disabled, hasPsk: false);

            // Act
            var transmitChannels = _manager.GetTransmitChannels().ToList();

            // Assert
            transmitChannels.Should().HaveCount(2);
            transmitChannels.Should().NotContain(c => c.Index == 2);
        }

        [Fact]
        public void GetTransmitChannels_ExcludesAdminChannels()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "admin", ChannelRole.Secondary, hasPsk: true);

            // Act
            var transmitChannels = _manager.GetTransmitChannels().ToList();

            // Assert - SEC-02 compliance
            transmitChannels.Should().HaveCount(1);
            transmitChannels[0].Index.Should().Be(0);
        }

        [Fact]
        public void GetTransmitChannels_ReturnsOrderedByIndex()
        {
            // Arrange - add out of order
            _manager.UpdateChannel(3, "Third", ChannelRole.Secondary, hasPsk: true);
            _manager.UpdateChannel(0, "First", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Second", ChannelRole.Secondary, hasPsk: true);

            // Act
            var transmitChannels = _manager.GetTransmitChannels().ToList();

            // Assert
            transmitChannels.Should().HaveCount(3);
            transmitChannels[0].Index.Should().Be(0);
            transmitChannels[1].Index.Should().Be(1);
            transmitChannels[2].Index.Should().Be(3);
        }

        #endregion

        #region SetReceiveEnabled Tests (CHN-03)

        [Fact]
        public void SetReceiveEnabled_True_EnablesReceive()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: true);

            // Act
            _manager.SetReceiveEnabled(0, true);

            // Assert
            _manager.GetChannel(0)!.ReceiveEnabled.Should().BeTrue();
        }

        [Fact]
        public void SetReceiveEnabled_False_DisablesReceive()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: true);

            // Act
            _manager.SetReceiveEnabled(0, false);

            // Assert
            _manager.GetChannel(0)!.ReceiveEnabled.Should().BeFalse();
        }

        [Fact]
        public void SetReceiveEnabled_RaisesChannelChangedEvent()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: true);

            ChannelChangedEventArgs? eventArgs = null;
            _manager.ChannelChanged += (sender, args) => eventArgs = args;

            // Act
            _manager.SetReceiveEnabled(0, false);

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs!.Channel.ReceiveEnabled.Should().BeFalse();
        }

        [Fact]
        public void SetReceiveEnabled_DisabledChannel_NoEffect()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Disabled, hasPsk: false);

            // Act
            _manager.SetReceiveEnabled(0, true);

            // Assert - Disabled channels stay disabled
            _manager.GetChannel(0)!.ReceiveEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsReceiveEnabled_ExistingChannel_ReturnsState()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: true);
            _manager.SetReceiveEnabled(0, false);

            // Act & Assert
            _manager.IsReceiveEnabled(0).Should().BeFalse();
        }

        [Fact]
        public void IsReceiveEnabled_UnknownChannel_ReturnsTrue()
        {
            // Act & Assert - unknown channels default to enabled
            _manager.IsReceiveEnabled(99).Should().BeTrue();
        }

        #endregion

        #region SetTransmitEnabled Tests

        [Fact]
        public void SetTransmitEnabled_AdminChannel_CannotEnable()
        {
            // Arrange
            _manager.UpdateChannel(0, "admin", ChannelRole.Primary, hasPsk: true);

            // Act - try to enable transmit on admin channel
            _manager.SetTransmitEnabled(0, true);

            // Assert - SEC-02: Admin channel transmit cannot be enabled
            _manager.GetChannel(0)!.TransmitEnabled.Should().BeFalse();
        }

        [Fact]
        public void SetTransmitEnabled_NonAdminChannel_CanEnable()
        {
            // Arrange
            _manager.UpdateChannel(0, "Test", ChannelRole.Primary, hasPsk: true);
            _manager.SetTransmitEnabled(0, false);

            // Act
            _manager.SetTransmitEnabled(0, true);

            // Assert
            _manager.GetChannel(0)!.TransmitEnabled.Should().BeTrue();
        }

        #endregion

        #region SelectedOutboundChannel Tests

        [Fact]
        public void SelectedOutboundChannel_DefaultsToZero()
        {
            // Assert - CHN-06: Default outbound channel is 0
            _manager.SelectedOutboundChannel.Should().Be(0);
        }

        [Fact]
        public void SelectedOutboundChannel_Set_UpdatesValue()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Secondary", ChannelRole.Secondary, hasPsk: true);

            // Act
            _manager.SelectedOutboundChannel = 1;

            // Assert
            _manager.SelectedOutboundChannel.Should().Be(1);
        }

        [Fact]
        public void SelectedOutboundChannel_SetInvalidChannel_NoChange()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);

            // Act - try to select non-existent channel
            _manager.SelectedOutboundChannel = 99;

            // Assert - should remain at 0
            _manager.SelectedOutboundChannel.Should().Be(0);
        }

        [Fact]
        public void SelectedOutboundChannel_SetDisabledChannel_NoChange()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Disabled", ChannelRole.Disabled, hasPsk: false);

            // Act
            _manager.SelectedOutboundChannel = 1;

            // Assert
            _manager.SelectedOutboundChannel.Should().Be(0);
        }

        #endregion

        #region GetReceiveChannels Tests

        [Fact]
        public void GetReceiveChannels_OnlyReturnsEnabledChannels()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Secondary", ChannelRole.Secondary, hasPsk: true);
            _manager.SetReceiveEnabled(1, false);

            // Act
            var receiveChannels = _manager.GetReceiveChannels().ToList();

            // Assert - CHN-03 compliance
            receiveChannels.Should().HaveCount(1);
            receiveChannels[0].Index.Should().Be(0);
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_RemovesAllChannels()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Secondary", ChannelRole.Secondary, hasPsk: true);
            _manager.SelectedOutboundChannel = 1;

            // Act
            _manager.Clear();

            // Assert
            _manager.GetAllChannels().Should().BeEmpty();
            _manager.SelectedOutboundChannel.Should().Be(0);
        }

        #endregion

        #region GetPrimaryChannel Tests

        [Fact]
        public void GetPrimaryChannel_ReturnsFirstPrimaryChannel()
        {
            // Arrange
            _manager.UpdateChannel(0, "Primary", ChannelRole.Primary, hasPsk: true);
            _manager.UpdateChannel(1, "Secondary", ChannelRole.Secondary, hasPsk: true);

            // Act
            var primary = _manager.GetPrimaryChannel();

            // Assert
            primary.Should().NotBeNull();
            primary!.Index.Should().Be(0);
            primary.Role.Should().Be(ChannelRole.Primary);
        }

        [Fact]
        public void GetPrimaryChannel_NoPrimary_ReturnsNull()
        {
            // Arrange
            _manager.UpdateChannel(0, "Secondary", ChannelRole.Secondary, hasPsk: true);

            // Act
            var primary = _manager.GetPrimaryChannel();

            // Assert
            primary.Should().BeNull();
        }

        #endregion

        #region TeamColor Tests

        [Theory]
        [InlineData(0, "Cyan")]
        [InlineData(1, "Green")]
        [InlineData(2, "Yellow")]
        [InlineData(3, "Orange")]
        [InlineData(4, "Red")]
        [InlineData(5, "Purple")]
        [InlineData(6, "White")]
        [InlineData(7, "Magenta")]
        public void UpdateChannel_SetsCorrectTeamColor(int channelIndex, string expectedColor)
        {
            // Act
            _manager.UpdateChannel(channelIndex, "Test", ChannelRole.Secondary, hasPsk: false);

            // Assert
            var channel = _manager.GetChannel(channelIndex);
            channel.Should().NotBeNull();
            channel!.TeamColor.Should().Be(expectedColor);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void UpdateChannel_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var tasks = new System.Threading.Tasks.Task[10];

            // Act - concurrent updates
            for (int i = 0; i < 10; i++)
            {
                int channelIndex = i % 8;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    _manager.UpdateChannel(channelIndex, $"Channel {channelIndex}", ChannelRole.Secondary, hasPsk: false);
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert - no exceptions and data is consistent
            var channels = _manager.GetAllChannels().ToList();
            channels.Should().NotBeEmpty();
        }

        #endregion
    }
}
