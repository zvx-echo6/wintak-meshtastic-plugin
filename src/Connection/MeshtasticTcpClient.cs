using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Logging;

namespace WinTakMeshtasticPlugin.Connection
{
    /// <summary>
    /// TCP client for communicating with a Meshtastic node.
    /// Handles connection, reconnection, and packet deserialization.
    /// All I/O runs on background threads per threading rules.
    /// </summary>
    public class MeshtasticTcpClient : IMeshtasticClient, IDisposable
    {
        private readonly ILogger<MeshtasticTcpClient>? _logger;
        private readonly MeshtasticClientConfig _config;

        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;

        private ConnectionState _state = ConnectionState.Disconnected;
        private DateTime _lastPacketTime = DateTime.MinValue;

        /// <summary>
        /// Unique identifier for this connection.
        /// Used to key node state for multi-node support.
        /// </summary>
        public string ConnectionId { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Current connection state.
        /// </summary>
        public ConnectionState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    var old = _state;
                    _state = value;
                    StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(old, value));
                }
            }
        }

        /// <summary>
        /// Event raised when a MeshPacket is received.
        /// </summary>
        public event EventHandler<MeshPacketReceivedEventArgs>? PacketReceived;

        /// <summary>
        /// Event raised when connection state changes.
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

        public MeshtasticTcpClient(MeshtasticClientConfig config, ILogger<MeshtasticTcpClient>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
        }

        /// <summary>
        /// Connect to the Meshtastic node and start the read loop.
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (State == ConnectionState.Connected)
            {
                _logger?.LogWarning("Already connected to {Host}:{Port}", _config.Hostname, _config.Port);
                return;
            }

            State = ConnectionState.Connecting;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_config.Hostname, _config.Port).ConfigureAwait(false);
                _stream = _client.GetStream();

                State = ConnectionState.Connected;
                _logger?.LogInformation("Connected to Meshtastic node at {Host}:{Port}",
                    _config.Hostname, _config.Port);

                // Start background read loop
                _readLoopTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to connect to {Host}:{Port}", _config.Hostname, _config.Port);
                State = ConnectionState.Disconnected;
                throw;
            }
        }

        /// <summary>
        /// Disconnect from the Meshtastic node.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _logger?.LogInformation("Disconnecting from Meshtastic node");

            _cts?.Cancel();

            if (_readLoopTask != null)
            {
                try
                {
                    await _readLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            Cleanup();
            State = ConnectionState.Disconnected;
        }

        /// <summary>
        /// Send a MeshPacket to the Meshtastic node.
        /// </summary>
        public async Task SendPacketAsync(MeshPacket packet, CancellationToken cancellationToken = default)
        {
            if (State != ConnectionState.Connected || _stream == null)
            {
                throw new InvalidOperationException("Not connected to Meshtastic node");
            }

            // Wrap in ToRadio message
            var toRadio = new ToRadio { Packet = packet };
            var data = toRadio.ToByteArray();

            // Meshtastic TCP protocol: 4-byte header (0x94 0xc3 + 2-byte length) + protobuf data
            var header = new byte[4];
            header[0] = 0x94;
            header[1] = 0xc3;
            header[2] = (byte)((data.Length >> 8) & 0xFF);
            header[3] = (byte)(data.Length & 0xFF);

            await _stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger?.LogDebug("Sent packet to Meshtastic node");
        }

        /// <summary>
        /// Background read loop that deserializes incoming MeshPackets.
        /// </summary>
        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var headerBuffer = new byte[4];

            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                try
                {
                    // Read 4-byte header: magic bytes (0x94 0xc3) + 2-byte length
                    var headerBytesRead = await ReadExactAsync(_stream, headerBuffer, 4, cancellationToken)
                        .ConfigureAwait(false);

                    if (headerBytesRead < 4)
                    {
                        _logger?.LogWarning("Connection closed while reading header");
                        break;
                    }

                    // Validate magic bytes
                    if (headerBuffer[0] != 0x94 || headerBuffer[1] != 0xc3)
                    {
                        _logger?.LogWarning("Invalid magic bytes: {B0:X2} {B1:X2}",
                            headerBuffer[0], headerBuffer[1]);
                        continue;
                    }

                    // Parse length (big-endian)
                    int length = (headerBuffer[2] << 8) | headerBuffer[3];

                    if (length <= 0 || length > 65535)
                    {
                        _logger?.LogWarning("Invalid packet length: {Length}", length);
                        continue;
                    }

                    // Read packet data
                    var dataBuffer = new byte[length];
                    var dataBytesRead = await ReadExactAsync(_stream, dataBuffer, length, cancellationToken)
                        .ConfigureAwait(false);

                    if (dataBytesRead < length)
                    {
                        _logger?.LogWarning("Connection closed while reading packet data");
                        break;
                    }

                    // Deserialize FromRadio message
                    try
                    {
                        var fromRadio = FromRadio.Parser.ParseFrom(dataBuffer);
                        _lastPacketTime = DateTime.UtcNow;

                        // Extract MeshPacket if present
                        if (fromRadio.PayloadVariantCase == FromRadio.PayloadVariantOneofCase.Packet)
                        {
                            var packet = fromRadio.Packet;
                            _logger?.LogDebug("Received MeshPacket from node {NodeId:X8}, portnum {PortNum}",
                                packet.From, packet.Decoded?.Portnum);

                            PacketReceived?.Invoke(this, new MeshPacketReceivedEventArgs(packet, ConnectionId));
                        }
                        else
                        {
                            _logger?.LogDebug("Received FromRadio with payload type: {Type}",
                                fromRadio.PayloadVariantCase);
                        }
                    }
                    catch (InvalidProtocolBufferException ex)
                    {
                        // Log malformed packets at Warning level per CLAUDE.md, but don't crash
                        _logger?.LogWarning(ex, "Failed to parse protobuf packet");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "Read error, will attempt reconnection");
                    break;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogError(ex, "Unexpected error in read loop");
                    break;
                }
            }

            // Connection lost - attempt reconnection if not cancelled
            if (!cancellationToken.IsCancellationRequested)
            {
                _ = Task.Run(() => ReconnectLoopAsync(cancellationToken), cancellationToken);
            }
        }

        /// <summary>
        /// Reconnection loop with configurable retry interval.
        /// </summary>
        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            Cleanup();
            State = ConnectionState.Reconnecting;

            while (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogInformation("Attempting reconnection in {Interval} seconds",
                    _config.ReconnectIntervalSeconds);

                await Task.Delay(TimeSpan.FromSeconds(_config.ReconnectIntervalSeconds), cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    _client = new TcpClient();
                    await _client.ConnectAsync(_config.Hostname, _config.Port).ConfigureAwait(false);
                    _stream = _client.GetStream();

                    State = ConnectionState.Connected;
                    _logger?.LogInformation("Reconnected to Meshtastic node at {Host}:{Port}",
                        _config.Hostname, _config.Port);

                    // Restart read loop
                    _readLoopTask = Task.Run(() => ReadLoopAsync(cancellationToken), cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Reconnection attempt failed");
                    Cleanup();
                }
            }
        }

        /// <summary>
        /// Read exactly the specified number of bytes from the stream.
        /// </summary>
        private static async Task<int> ReadExactAsync(
            NetworkStream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    return totalRead; // Connection closed
                }

                totalRead += bytesRead;
            }
            return totalRead;
        }

        private void Cleanup()
        {
            _stream?.Dispose();
            _stream = null;
            _client?.Dispose();
            _client = null;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            Cleanup();
            _cts?.Dispose();
        }
    }

    /// <summary>
    /// Configuration for the Meshtastic TCP client.
    /// </summary>
    public class MeshtasticClientConfig
    {
        /// <summary>
        /// Hostname or IP address of the Meshtastic node.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// TCP port of the Meshtastic node. Default: 4403
        /// </summary>
        public int Port { get; set; } = 4403;

        /// <summary>
        /// Reconnect retry interval in seconds. Range: 5-60, Default: 15
        /// </summary>
        public int ReconnectIntervalSeconds
        {
            get => _reconnectIntervalSeconds;
            set => _reconnectIntervalSeconds = Math.Clamp(value, 5, 60);
        }
        private int _reconnectIntervalSeconds = 15;
    }

    /// <summary>
    /// Connection state enumeration.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// Event args for MeshPacket received events.
    /// </summary>
    public class MeshPacketReceivedEventArgs : EventArgs
    {
        public MeshPacket Packet { get; }
        public string ConnectionId { get; }

        public MeshPacketReceivedEventArgs(MeshPacket packet, string connectionId)
        {
            Packet = packet;
            ConnectionId = connectionId;
        }
    }

    /// <summary>
    /// Event args for connection state change events.
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionState OldState { get; }
        public ConnectionState NewState { get; }

        public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Interface for the Meshtastic client to support testing.
    /// </summary>
    public interface IMeshtasticClient
    {
        string ConnectionId { get; }
        ConnectionState State { get; }
        event EventHandler<MeshPacketReceivedEventArgs>? PacketReceived;
        event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        Task SendPacketAsync(MeshPacket packet, CancellationToken cancellationToken = default);
    }
}
