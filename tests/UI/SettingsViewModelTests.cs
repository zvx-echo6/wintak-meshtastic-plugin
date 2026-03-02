using System;
using System.ComponentModel;
using FluentAssertions;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.Models;
using WinTakMeshtasticPlugin.UI;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.UI
{
    public class SettingsViewModelTests
    {
        private PluginSettings CreateSettings() => new PluginSettings();

        private ChannelManager CreateChannelManager() => new ChannelManager();

        private SettingsViewModel CreateViewModel(
            PluginSettings settings = null,
            ChannelManager channelManager = null)
        {
            settings ??= CreateSettings();
            channelManager ??= CreateChannelManager();

            return new SettingsViewModel(
                settings,
                channelManager,
                (hostname, port) => { }, // Connect action (no-op for tests)
                () => { }, // Disconnect action (no-op for tests)
                () => 0); // GetNodeCount (returns 0 for tests)
        }

        [Fact]
        public void AutoConnect_InitializesFromSettings()
        {
            // Arrange
            var settings = new PluginSettings { AutoConnect = true };

            // Act
            var viewModel = CreateViewModel(settings);

            // Assert
            viewModel.AutoConnect.Should().BeTrue();
        }

        [Fact]
        public void AutoConnect_DefaultsToFalse()
        {
            // Arrange & Act
            var viewModel = CreateViewModel();

            // Assert
            viewModel.AutoConnect.Should().BeFalse();
        }

        [Fact]
        public void AutoConnect_Set_UpdatesSettings()
        {
            // Arrange
            var settings = new PluginSettings { AutoConnect = false };
            var viewModel = CreateViewModel(settings);

            // Act
            viewModel.AutoConnect = true;

            // Assert
            settings.AutoConnect.Should().BeTrue();
        }

        [Fact]
        public void AutoConnect_Set_RaisesPropertyChanged()
        {
            // Arrange
            var viewModel = CreateViewModel();
            string propertyChanged = null;
            viewModel.PropertyChanged += (s, e) => propertyChanged = e.PropertyName;

            // Act
            viewModel.AutoConnect = true;

            // Assert
            propertyChanged.Should().Be(nameof(viewModel.AutoConnect));
        }

        [Fact]
        public void AutoConnect_SetSameValue_DoesNotRaisePropertyChanged()
        {
            // Arrange
            var settings = new PluginSettings { AutoConnect = true };
            var viewModel = CreateViewModel(settings);

            bool eventRaised = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.AutoConnect))
                    eventRaised = true;
            };

            // Act
            viewModel.AutoConnect = true; // Same value

            // Assert
            eventRaised.Should().BeFalse();
        }

        [Fact]
        public void Hostname_InitializesFromSettings()
        {
            // Arrange
            var settings = new PluginSettings { Hostname = "192.168.1.100" };

            // Act
            var viewModel = CreateViewModel(settings);

            // Assert
            viewModel.Hostname.Should().Be("192.168.1.100");
        }

        [Fact]
        public void Hostname_Set_UpdatesSettings()
        {
            // Arrange
            var settings = new PluginSettings();
            var viewModel = CreateViewModel(settings);

            // Act
            viewModel.Hostname = "meshtastic.local";

            // Assert
            settings.Hostname.Should().Be("meshtastic.local");
        }

        [Fact]
        public void Port_InitializesFromSettings()
        {
            // Arrange
            var settings = new PluginSettings { Port = 5000 };

            // Act
            var viewModel = CreateViewModel(settings);

            // Assert
            viewModel.Port.Should().Be(5000);
        }

        [Fact]
        public void Port_Set_ClampsToValidRange()
        {
            // Arrange
            var settings = new PluginSettings();
            var viewModel = CreateViewModel(settings);

            // Act
            viewModel.Port = 99999;

            // Assert
            viewModel.Port.Should().Be(65535);
            settings.Port.Should().Be(65535);
        }

        [Fact]
        public void ReconnectIntervalSeconds_InitializesFromSettings()
        {
            // Arrange
            var settings = new PluginSettings { ReconnectIntervalSeconds = 30 };

            // Act
            var viewModel = CreateViewModel(settings);

            // Assert
            viewModel.ReconnectIntervalSeconds.Should().Be(30);
        }

        [Fact]
        public void ReconnectIntervalSeconds_Set_ClampsToValidRange()
        {
            // Arrange
            var settings = new PluginSettings();
            var viewModel = CreateViewModel(settings);

            // Act
            viewModel.ReconnectIntervalSeconds = 1;

            // Assert
            viewModel.ReconnectIntervalSeconds.Should().Be(5);
        }

        [Fact]
        public void SaveSettingsCommand_SavesSettings()
        {
            // Arrange
            var settings = new PluginSettings { AutoConnect = true };
            var viewModel = CreateViewModel(settings);

            // Act - The command saves settings to disk
            // We verify the settings object has been validated
            viewModel.SaveSettingsCommand.Execute(null);

            // Assert - After save, settings should be valid
            settings.Port.Should().BeInRange(1, 65535);
            settings.ReconnectIntervalSeconds.Should().BeInRange(5, 60);
        }

        [Fact]
        public void ConnectCommand_CanExecute_WhenDisconnected()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.ConnectionState = ConnectionState.Disconnected;

            // Act & Assert
            viewModel.ConnectCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void ConnectCommand_CannotExecute_WhenConnecting()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.ConnectionState = ConnectionState.Connecting;

            // Act & Assert
            viewModel.ConnectCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void IsNotConnected_TrueWhenDisconnected()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.ConnectionState = ConnectionState.Disconnected;

            // Assert
            viewModel.IsNotConnected.Should().BeTrue();
            viewModel.IsConnected.Should().BeFalse();
        }

        [Fact]
        public void IsConnected_TrueWhenConnected()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.ConnectionState = ConnectionState.Connected;

            // Assert
            viewModel.IsConnected.Should().BeTrue();
            viewModel.IsNotConnected.Should().BeFalse();
        }
    }
}
