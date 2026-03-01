using System;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handler for TEXT_MESSAGE_APP (portnum 1) packets.
    /// Routes messages to channel-specific chat groups via IChatService.
    /// Messages from different channels MUST NOT appear in the same chat window (MSG-02).
    /// </summary>
    public class TextMessageHandler : IPacketHandler
    {
        /// <inheritdoc />
        public PortNum HandledPortNum => PortNum.TextMessageApp;

        /// <summary>
        /// Event raised when a text message is received.
        /// Used to route messages to the appropriate chat group.
        /// </summary>
        public event EventHandler<TextMessageReceivedEventArgs>? MessageReceived;

        /// <inheritdoc />
        public Task<PacketHandlerResult?> HandleAsync(MeshPacket packet, PacketHandlerContext context)
        {
            if (packet.Decoded?.Payload == null || packet.Decoded.Payload.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("[TextMessageHandler] Empty payload, skipping");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // TEXT_MESSAGE_APP payload is raw UTF-8 text, not a protobuf message
            string messageText;
            try
            {
                messageText = Encoding.UTF8.GetString(packet.Decoded.Payload.ToByteArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextMessageHandler] Warning: Failed to decode text: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            if (string.IsNullOrEmpty(messageText))
            {
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Get sender info
            uint senderNodeId = packet.From;
            int channelIndex = (int)packet.Channel;

            // Look up sender's shortname if available
            string senderCallsign = GetSenderCallsign(context, senderNodeId);

            // Generate sender UID for CoT
            string senderUid = $"MESH-{context.ConnectionId}-{senderNodeId:X8}";

            // Generate channel-specific chat room name (MSG-02: different channels = different windows)
            string chatRoom = GetChannelChatRoomName(context, channelIndex);

            System.Diagnostics.Debug.WriteLine(
                $"[TextMessageHandler] Message from {senderCallsign} on channel {channelIndex}: {messageText}");

            // Raise event for chat routing
            MessageReceived?.Invoke(this, new TextMessageReceivedEventArgs
            {
                SenderNodeId = senderNodeId,
                SenderCallsign = senderCallsign,
                SenderUid = senderUid,
                ChannelIndex = channelIndex,
                ChatRoom = chatRoom,
                Message = messageText,
                Timestamp = DateTime.UtcNow,
                ConnectionId = context.ConnectionId
            });

            // Build GeoChat CoT for the message
            if (context.CotBuilder == null)
            {
                return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
                {
                    CotXml = null,
                    UpdatesNodeState = false,
                    DebugMessage = $"Message from {senderCallsign}: {messageText}"
                });
            }

            try
            {
                string cotXml = context.CotBuilder.BuildGeoChat(
                    senderUid,
                    senderCallsign,
                    messageText,
                    chatRoom);

                return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
                {
                    CotXml = cotXml,
                    UpdatesNodeState = false,
                    DebugMessage = $"Message from {senderCallsign}: {messageText}"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextMessageHandler] Failed to build CoT: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }
        }

        /// <summary>
        /// Get the sender's callsign from node state, falling back to hex ID.
        /// </summary>
        private static string GetSenderCallsign(PacketHandlerContext context, uint nodeId)
        {
            if (context.NodeStateManager != null)
            {
                var nodeState = context.NodeStateManager.Get(context.ConnectionId, nodeId);
                if (nodeState != null && !string.IsNullOrEmpty(nodeState.ShortName))
                {
                    return nodeState.ShortName;
                }
            }

            // Fall back to hex node ID
            return $"!{nodeId:x8}";
        }

        /// <summary>
        /// Get the chat room name for a channel.
        /// Each channel gets its own chat room to ensure MSG-02 compliance.
        /// </summary>
        private static string GetChannelChatRoomName(PacketHandlerContext context, int channelIndex)
        {
            if (context.ChannelManager != null)
            {
                var channel = context.ChannelManager.GetChannel(channelIndex);
                if (channel != null && !string.IsNullOrEmpty(channel.Name))
                {
                    return $"Mesh: {channel.Name}";
                }
            }

            return $"Mesh: Channel {channelIndex}";
        }
    }

    /// <summary>
    /// Event args for text message received events.
    /// </summary>
    public class TextMessageReceivedEventArgs : EventArgs
    {
        public uint SenderNodeId { get; set; }
        public string SenderCallsign { get; set; } = string.Empty;
        public string SenderUid { get; set; } = string.Empty;
        public int ChannelIndex { get; set; }
        public string ChatRoom { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ConnectionId { get; set; } = string.Empty;
    }
}
