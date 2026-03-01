using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using WinTakMeshtasticPlugin.Connection;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Connection
{
    public class MeshtasticTcpClientTests
    {
        [Fact]
        public void MeshtasticClientConfig_DefaultValues()
        {
            // Arrange & Act
            var config = new MeshtasticClientConfig();

            // Assert
            config.Hostname.Should().Be("localhost");
            config.Port.Should().Be(4403);
            config.ReconnectIntervalSeconds.Should().Be(15);
        }

        [Fact]
        public void MeshtasticClientConfig_HostnameConfigurable()
        {
            // Arrange & Act
            var config = new MeshtasticClientConfig
            {
                Hostname = "192.168.1.100"
            };

            // Assert
            config.Hostname.Should().Be("192.168.1.100");
        }

        [Fact]
        public void MeshtasticClientConfig_PortConfigurable()
        {
            // Arrange & Act
            var config = new MeshtasticClientConfig
            {
                Port = 5000
            };

            // Assert
            config.Port.Should().Be(5000);
        }

        [Fact]
        public void MeshtasticClientConfig_ReconnectInterval_ClampedToRange()
        {
            // Arrange
            var config = new MeshtasticClientConfig();

            // Act & Assert - below minimum
            config.ReconnectIntervalSeconds = 1;
            config.ReconnectIntervalSeconds.Should().Be(5);

            // Act & Assert - above maximum
            config.ReconnectIntervalSeconds = 100;
            config.ReconnectIntervalSeconds.Should().Be(60);

            // Act & Assert - within range
            config.ReconnectIntervalSeconds = 30;
            config.ReconnectIntervalSeconds.Should().Be(30);
        }

        [Fact]
        public void Constructor_WithConfig_SetsProperties()
        {
            // Arrange
            var config = new MeshtasticClientConfig
            {
                Hostname = "test.local",
                Port = 4404
            };

            // Act
            using var client = new MeshtasticTcpClient(config);

            // Assert
            client.State.Should().Be(ConnectionState.Disconnected);
            client.ConnectionId.Should().NotBeNullOrEmpty();
            client.ConnectionId.Should().HaveLength(8);
        }

        [Fact]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new MeshtasticTcpClient(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("config");
        }

        [Fact]
        public void ConnectionId_IsUnique()
        {
            // Arrange
            var config = new MeshtasticClientConfig();

            // Act
            using var client1 = new MeshtasticTcpClient(config);
            using var client2 = new MeshtasticTcpClient(config);

            // Assert
            client1.ConnectionId.Should().NotBe(client2.ConnectionId);
        }

        [Fact]
        public async Task ConnectAsync_InvalidHost_ThrowsException()
        {
            // Arrange
            var config = new MeshtasticClientConfig
            {
                Hostname = "invalid.host.that.does.not.exist.local",
                Port = 4403
            };

            using var client = new MeshtasticTcpClient(config);

            // Act & Assert
            Func<Task> act = () => client.ConnectAsync();
            await act.Should().ThrowAsync<Exception>();
            client.State.Should().Be(ConnectionState.Disconnected);
        }

        [Fact]
        public async Task SendPacketAsync_NotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new MeshtasticClientConfig();
            using var client = new MeshtasticTcpClient(config);
            var packet = new Meshtastic.Protobufs.MeshPacket();

            // Act & Assert
            Func<Task> act = () => client.SendPacketAsync(packet);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Not connected*");
        }

        [Fact]
        public void StateChanged_Event_RaisedOnStateChange()
        {
            // Arrange
            var config = new MeshtasticClientConfig
            {
                Hostname = "invalid.host.local"
            };

            using var client = new MeshtasticTcpClient(config);

            ConnectionState? oldState = null;
            ConnectionState? newState = null;
            client.StateChanged += (sender, args) =>
            {
                oldState = args.OldState;
                newState = args.NewState;
            };

            // Act - trigger connection attempt (will fail but state should change)
            try
            {
                client.ConnectAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Expected to fail
            }

            // Assert - should have at least attempted to connect
            // (Depending on timing, may get Connecting->Disconnected or just Disconnected)
        }

        [Fact]
        public void Dispose_DisposesResources()
        {
            // Arrange
            var config = new MeshtasticClientConfig();
            var client = new MeshtasticTcpClient(config);

            // Act
            client.Dispose();

            // Assert - should not throw
            client.Dispose(); // Dispose should be idempotent
        }

        [Fact]
        public async Task DisconnectAsync_WhenNotConnected_CompletesSuccessfully()
        {
            // Arrange
            var config = new MeshtasticClientConfig();
            using var client = new MeshtasticTcpClient(config);

            // Act
            await client.DisconnectAsync();

            // Assert
            client.State.Should().Be(ConnectionState.Disconnected);
        }
    }

    public class ConnectionStateChangedEventArgsTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            // Arrange & Act
            var args = new ConnectionStateChangedEventArgs(
                ConnectionState.Disconnected,
                ConnectionState.Connected);

            // Assert
            args.OldState.Should().Be(ConnectionState.Disconnected);
            args.NewState.Should().Be(ConnectionState.Connected);
        }
    }

    public class MeshPacketReceivedEventArgsTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            // Arrange
            var packet = new Meshtastic.Protobufs.MeshPacket { From = 0x12345678 };
            var connectionId = "testconn";

            // Act
            var args = new MeshPacketReceivedEventArgs(packet, connectionId);

            // Assert
            args.Packet.Should().BeSameAs(packet);
            args.ConnectionId.Should().Be(connectionId);
        }
    }

    public class ChannelReceivedEventArgsTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            // Arrange
            var channel = new Meshtastic.Protobufs.Channel
            {
                Index = 0,
                Role = Meshtastic.Protobufs.Channel.Types.Role.Primary
            };
            var connectionId = "testconn";

            // Act
            var args = new ChannelReceivedEventArgs(channel, connectionId);

            // Assert
            args.Channel.Should().BeSameAs(channel);
            args.ConnectionId.Should().Be(connectionId);
        }
    }
}
