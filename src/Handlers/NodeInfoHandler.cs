using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handler for NODEINFO_APP (portnum 4) packets.
    /// Stores node identity information (shortname, longname, hardware, firmware).
    /// Updates existing node markers with resolved shortnames.
    /// </summary>
    public class NodeInfoHandler : IPacketHandler
    {
        /// <inheritdoc />
        public PortNum HandledPortNum => PortNum.NodeinfoApp;

        /// <inheritdoc />
        public Task<PacketHandlerResult?> HandleAsync(MeshPacket packet, PacketHandlerContext context)
        {
            if (packet.Decoded?.Payload == null || packet.Decoded.Payload.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("[NodeInfoHandler] Empty payload, skipping");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            User user;
            try
            {
                user = User.Parser.ParseFrom(packet.Decoded.Payload);
            }
            catch (InvalidProtocolBufferException ex)
            {
                // Log malformed packets at Warning level per CLAUDE.md
                System.Diagnostics.Debug.WriteLine($"[NodeInfoHandler] Warning: Failed to parse User: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Get or create node state
            if (context.NodeStateManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[NodeInfoHandler] Warning: No NodeStateManager in context");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            var nodeState = context.NodeStateManager.GetOrCreate(context.ConnectionId, packet.From);

            // Track if this is a significant update that warrants a CoT refresh
            bool hasPosition = nodeState.Latitude.HasValue && nodeState.Longitude.HasValue;
            bool shortNameChanged = !string.Equals(nodeState.ShortName, user.ShortName, StringComparison.Ordinal);

            // Update node identity information
            if (!string.IsNullOrEmpty(user.ShortName))
            {
                nodeState.ShortName = user.ShortName;
            }

            if (!string.IsNullOrEmpty(user.LongName))
            {
                nodeState.LongName = user.LongName;
            }

            // Hardware model enum to string
            if (user.HwModel != HardwareModel.Unset)
            {
                nodeState.HardwareModel = user.HwModel.ToString();
            }

            // Device role mapping
            nodeState.Role = MapRole(user.Role);

            nodeState.LastHeard = DateTime.UtcNow;

            // Track channel membership
            if (packet.Channel >= 0 && packet.Channel < 8)
            {
                nodeState.ChannelsMembership.Add((int)packet.Channel);
            }

            // Save updated state
            context.NodeStateManager.Update(nodeState);

            System.Diagnostics.Debug.WriteLine(
                $"[NodeInfoHandler] NodeInfo for {nodeState.NodeIdHex}: short=\"{user.ShortName}\", " +
                $"long=\"{user.LongName}\", hw={user.HwModel}, role={user.Role}");

            // If the node has position and shortname changed, regenerate CoT to update callsign
            if (hasPosition && shortNameChanged && context.CotBuilder != null)
            {
                try
                {
                    string cotXml = context.CotBuilder.BuildNodePli(nodeState);
                    return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
                    {
                        CotXml = cotXml,
                        UpdatesNodeState = true,
                        DebugMessage = $"NodeInfo: Updated callsign to {nodeState.DisplayName}"
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NodeInfoHandler] Failed to build CoT: {ex.Message}");
                }
            }

            // Return result without CoT if no position yet (will be generated when position arrives)
            return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
            {
                CotXml = null,
                UpdatesNodeState = true,
                DebugMessage = $"NodeInfo: {nodeState.DisplayName} (no position yet)"
            });
        }

        /// <summary>
        /// Map Meshtastic Config.DeviceConfig.Role to our DeviceRole enum.
        /// </summary>
        private static DeviceRole MapRole(Config.Types.DeviceConfig.Types.Role role)
        {
            return role switch
            {
                Config.Types.DeviceConfig.Types.Role.Client => DeviceRole.Client,
                Config.Types.DeviceConfig.Types.Role.ClientMute => DeviceRole.ClientMute,
                Config.Types.DeviceConfig.Types.Role.Router => DeviceRole.Router,
                Config.Types.DeviceConfig.Types.Role.RouterClient => DeviceRole.RouterClient,
                Config.Types.DeviceConfig.Types.Role.Repeater => DeviceRole.Repeater,
                Config.Types.DeviceConfig.Types.Role.Tracker => DeviceRole.Tracker,
                Config.Types.DeviceConfig.Types.Role.Sensor => DeviceRole.Sensor,
                Config.Types.DeviceConfig.Types.Role.Tak => DeviceRole.Tak,
                Config.Types.DeviceConfig.Types.Role.ClientHidden => DeviceRole.ClientHidden,
                Config.Types.DeviceConfig.Types.Role.LostAndFound => DeviceRole.LostAndFound,
                Config.Types.DeviceConfig.Types.Role.TakTracker => DeviceRole.TakTracker,
                _ => DeviceRole.Client
            };
        }
    }
}
