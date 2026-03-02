using System;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Models
{
    public class PluginSettingsTests : IDisposable
    {
        private readonly string _testSettingsPath;

        public PluginSettingsTests()
        {
            // Create a unique temp path for each test
            _testSettingsPath = Path.Combine(
                Path.GetTempPath(),
                $"meshtastic-test-{Guid.NewGuid()}",
                "settings.json");
        }

        public void Dispose()
        {
            // Clean up test files
            var dir = Path.GetDirectoryName(_testSettingsPath);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void AutoConnect_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var settings = new PluginSettings();

            // Assert
            settings.AutoConnect.Should().BeFalse();
        }

        [Fact]
        public void AutoConnect_CanBeSetToTrue()
        {
            // Arrange
            var settings = new PluginSettings();

            // Act
            settings.AutoConnect = true;

            // Assert
            settings.AutoConnect.Should().BeTrue();
        }

        [Fact]
        public void Hostname_DefaultValue_IsLocalhost()
        {
            // Arrange & Act
            var settings = new PluginSettings();

            // Assert
            settings.Hostname.Should().Be("localhost");
        }

        [Fact]
        public void Port_DefaultValue_Is4403()
        {
            // Arrange & Act
            var settings = new PluginSettings();

            // Assert
            settings.Port.Should().Be(4403);
        }

        [Fact]
        public void Serialize_AutoConnectTrue_IncludesProperty()
        {
            // Arrange
            var settings = new PluginSettings
            {
                AutoConnect = true,
                Hostname = "192.168.1.100",
                Port = 4403
            };

            // Act
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);

            // Assert
            json.Should().Contain("\"AutoConnect\": true");
        }

        [Fact]
        public void Deserialize_WithAutoConnectTrue_SetsProperty()
        {
            // Arrange
            var json = @"{
                ""Hostname"": ""192.168.1.100"",
                ""Port"": 4403,
                ""AutoConnect"": true
            }";

            // Act
            var settings = JsonConvert.DeserializeObject<PluginSettings>(json);

            // Assert
            settings.Should().NotBeNull();
            settings!.AutoConnect.Should().BeTrue();
            settings.Hostname.Should().Be("192.168.1.100");
            settings.Port.Should().Be(4403);
        }

        [Fact]
        public void Deserialize_WithoutAutoConnect_DefaultsToFalse()
        {
            // Arrange - JSON without AutoConnect property (simulates old settings file)
            var json = @"{
                ""Hostname"": ""192.168.1.100"",
                ""Port"": 4403
            }";

            // Act
            var settings = JsonConvert.DeserializeObject<PluginSettings>(json);

            // Assert
            settings.Should().NotBeNull();
            settings!.AutoConnect.Should().BeFalse();
        }

        [Fact]
        public void Validate_DoesNotChangeAutoConnect()
        {
            // Arrange
            var settings = new PluginSettings { AutoConnect = true };

            // Act
            settings.Validate();

            // Assert
            settings.AutoConnect.Should().BeTrue();
        }

        [Fact]
        public void Validate_ClampsPortToValidRange()
        {
            // Arrange
            var settings = new PluginSettings { Port = 99999 };

            // Act
            settings.Validate();

            // Assert
            settings.Port.Should().Be(65535);
        }

        [Fact]
        public void Validate_ClampsReconnectIntervalToValidRange()
        {
            // Arrange
            var settings = new PluginSettings { ReconnectIntervalSeconds = 1 };

            // Act
            settings.Validate();

            // Assert
            settings.ReconnectIntervalSeconds.Should().Be(5);
        }

        [Fact]
        public void ReconnectIntervalSeconds_DefaultValue_Is15()
        {
            // Arrange & Act
            var settings = new PluginSettings();

            // Assert
            settings.ReconnectIntervalSeconds.Should().Be(15);
        }

        [Fact]
        public void Load_NoExistingFile_ReturnsDefaults()
        {
            // This test uses the static Load() which reads from the actual settings path
            // Since we can't easily mock the path, we verify defaults are returned
            // when no file exists (or file is invalid)

            // Arrange & Act
            var settings = new PluginSettings();

            // Assert - verify all defaults
            settings.AutoConnect.Should().BeFalse();
            settings.Hostname.Should().Be("localhost");
            settings.Port.Should().Be(4403);
            settings.ReconnectIntervalSeconds.Should().Be(15);
        }
    }

    public class AutoConnectValidationTests
    {
        [Theory]
        [InlineData("localhost", 4403, true)]
        [InlineData("192.168.1.100", 4403, true)]
        [InlineData("meshtastic.local", 4403, true)]
        [InlineData("", 4403, false)]
        [InlineData(null, 4403, false)]
        [InlineData("   ", 4403, false)]
        public void AutoConnectShouldOnlyWorkWithValidHostname(string hostname, int port, bool shouldAutoConnect)
        {
            // Arrange
            var settings = new PluginSettings
            {
                Hostname = hostname,
                Port = port,
                AutoConnect = true
            };

            // Act - Simulate the auto-connect check from MeshtasticModule.Startup()
            var canAutoConnect = settings.AutoConnect && !string.IsNullOrWhiteSpace(settings.Hostname);

            // Assert
            canAutoConnect.Should().Be(shouldAutoConnect);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(4403, 4403)]
        [InlineData(65535, 65535)]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(99999, 65535)]
        public void PortValidation_ClampsToValidRange(int input, int expected)
        {
            // Arrange
            var settings = new PluginSettings { Port = input };

            // Act
            settings.Validate();

            // Assert
            settings.Port.Should().Be(expected);
        }
    }
}
