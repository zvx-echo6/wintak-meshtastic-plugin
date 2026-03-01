using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Messaging
{
    /// <summary>
    /// Service for sending outbound messages to the mesh network.
    /// </summary>
    public class OutboundMessageService : IOutboundMessageService
    {
        private readonly IMeshtasticClient _client;
        private readonly IChannelManager _channelManager;

        /// <summary>
        /// Maximum message length in bytes (MSG-03).
        /// Meshtastic protocol limit.
        /// </summary>
        public const int MaxMessageLength = 228;

        /// <summary>
        /// Event raised when a message is sent successfully.
        /// Used to echo the message back to the local chat window.
        /// </summary>
        public event EventHandler<OutboundMessageSentEventArgs>? MessageSent;

        public OutboundMessageService(IMeshtasticClient client, IChannelManager channelManager)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        }

        /// <summary>
        /// Send a text message to the selected outbound channel.
        /// </summary>
        /// <param name="message">Text message to send (max 228 bytes).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if sent successfully.</returns>
        public async Task<bool> SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            return await SendTextMessageToChannelAsync(
                message,
                _channelManager.SelectedOutboundChannel,
                cancellationToken);
        }

        /// <summary>
        /// Send a text message to a specific channel.
        /// </summary>
        /// <param name="message">Text message to send (max 228 bytes).</param>
        /// <param name="channelIndex">Target channel index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if sent successfully.</returns>
        public async Task<bool> SendTextMessageToChannelAsync(
            string message,
            int channelIndex,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
            {
                System.Diagnostics.Debug.WriteLine("[OutboundMessage] Cannot send empty message");
                return false;
            }

            if (_client.State != ConnectionState.Connected)
            {
                System.Diagnostics.Debug.WriteLine("[OutboundMessage] Cannot send: not connected");
                return false;
            }

            // Validate channel is available for transmit
            var channel = _channelManager.GetChannel(channelIndex);
            if (channel == null || !channel.TransmitEnabled)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OutboundMessage] Cannot send to channel {channelIndex}: not available for transmit");
                return false;
            }

            // Check message length (MSG-03)
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            if (messageBytes.Length > MaxMessageLength)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OutboundMessage] Message too long: {messageBytes.Length} bytes (max {MaxMessageLength})");
                return false;
            }

            try
            {
                // Create MeshPacket with TEXT_MESSAGE_APP portnum
                var packet = new MeshPacket
                {
                    // Broadcast to all nodes (0xFFFFFFFF)
                    To = 0xFFFFFFFF,
                    Channel = (uint)channelIndex,
                    WantAck = false,
                    Decoded = new Data
                    {
                        Portnum = PortNum.TextMessageApp,
                        Payload = ByteString.CopyFrom(messageBytes)
                    }
                };

                await _client.SendPacketAsync(packet, cancellationToken);

                System.Diagnostics.Debug.WriteLine(
                    $"[OutboundMessage] Sent message to channel {channelIndex}: {message}");

                // Raise event for local echo
                MessageSent?.Invoke(this, new OutboundMessageSentEventArgs
                {
                    Message = message,
                    ChannelIndex = channelIndex,
                    ChannelName = channel.DisplayName,
                    Timestamp = DateTime.UtcNow
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OutboundMessage] Failed to send: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send a direct message to a specific node.
        /// </summary>
        /// <param name="message">Text message to send.</param>
        /// <param name="destinationNodeId">Target node ID.</param>
        /// <param name="channelIndex">Channel to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if sent successfully.</returns>
        public async Task<bool> SendDirectMessageAsync(
            string message,
            uint destinationNodeId,
            int channelIndex,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (_client.State != ConnectionState.Connected)
            {
                return false;
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            if (messageBytes.Length > MaxMessageLength)
            {
                return false;
            }

            try
            {
                var packet = new MeshPacket
                {
                    To = destinationNodeId,
                    Channel = (uint)channelIndex,
                    WantAck = true, // Want ACK for direct messages
                    Decoded = new Data
                    {
                        Portnum = PortNum.TextMessageApp,
                        Payload = ByteString.CopyFrom(messageBytes)
                    }
                };

                await _client.SendPacketAsync(packet, cancellationToken);

                System.Diagnostics.Debug.WriteLine(
                    $"[OutboundMessage] Sent DM to {destinationNodeId:X8}: {message}");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OutboundMessage] Failed to send DM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the remaining byte count for a message.
        /// </summary>
        public int GetRemainingBytes(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return MaxMessageLength;
            }

            int usedBytes = Encoding.UTF8.GetByteCount(message);
            return Math.Max(0, MaxMessageLength - usedBytes);
        }
    }

    /// <summary>
    /// Interface for outbound message service.
    /// </summary>
    public interface IOutboundMessageService
    {
        Task<bool> SendTextMessageAsync(string message, CancellationToken cancellationToken = default);
        Task<bool> SendTextMessageToChannelAsync(string message, int channelIndex, CancellationToken cancellationToken = default);
        Task<bool> SendDirectMessageAsync(string message, uint destinationNodeId, int channelIndex, CancellationToken cancellationToken = default);
        int GetRemainingBytes(string message);
        event EventHandler<OutboundMessageSentEventArgs>? MessageSent;
    }

    /// <summary>
    /// Event args for outbound message sent events.
    /// </summary>
    public class OutboundMessageSentEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public int ChannelIndex { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
