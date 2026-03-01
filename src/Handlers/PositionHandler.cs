using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handler for POSITION_APP (portnum 3) packets.
    /// Deserializes position data and generates CoT PLI events.
    /// </summary>
    public class PositionHandler : IPacketHandler
    {
        /// <summary>
        /// Meshtastic latitude/longitude are stored as integers that need division by 1e7.
        /// </summary>
        private const double CoordinateScaleFactor = 1e-7;

        /// <inheritdoc />
        public PortNum HandledPortNum => PortNum.PositionApp;

        /// <inheritdoc />
        public Task<PacketHandlerResult?> HandleAsync(MeshPacket packet, PacketHandlerContext context)
        {
            if (packet.Decoded?.Payload == null || packet.Decoded.Payload.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("[PositionHandler] Empty payload, skipping");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            Position position;
            try
            {
                position = Position.Parser.ParseFrom(packet.Decoded.Payload);
            }
            catch (InvalidProtocolBufferException ex)
            {
                // Log malformed packets at Warning level per CLAUDE.md
                System.Diagnostics.Debug.WriteLine($"[PositionHandler] Warning: Failed to parse Position: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Validate position data - skip if no valid coordinates
            if (position.LatitudeI == 0 && position.LongitudeI == 0)
            {
                System.Diagnostics.Debug.WriteLine("[PositionHandler] Zero coordinates, skipping");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Convert from integer format to decimal degrees
            double latitude = position.LatitudeI * CoordinateScaleFactor;
            double longitude = position.LongitudeI * CoordinateScaleFactor;

            // Validate coordinate ranges
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PositionHandler] Warning: Invalid coordinates lat={latitude}, lon={longitude}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Get or create node state
            if (context.NodeStateManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[PositionHandler] Warning: No NodeStateManager in context");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            var nodeState = context.NodeStateManager.GetOrCreate(context.ConnectionId, packet.From);

            // Update position data
            nodeState.Latitude = latitude;
            nodeState.Longitude = longitude;
            nodeState.LastHeard = DateTime.UtcNow;
            nodeState.LastPositionUpdate = DateTime.UtcNow;

            // Altitude (convert from mm to m if present)
            if (position.Altitude != 0)
            {
                nodeState.Altitude = position.Altitude;
            }

            // Track channel membership
            if (packet.Channel >= 0 && packet.Channel < 8)
            {
                nodeState.ChannelsMembership.Add((int)packet.Channel);
            }

            // Save updated state
            context.NodeStateManager.Update(nodeState);

            // Build CoT XML
            if (context.CotBuilder == null)
            {
                System.Diagnostics.Debug.WriteLine("[PositionHandler] Warning: No CotBuilder in context");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            string cotXml;
            try
            {
                cotXml = context.CotBuilder.BuildNodePli(nodeState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PositionHandler] Failed to build CoT: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[PositionHandler] Position for {nodeState.DisplayName}: {latitude:F6}, {longitude:F6}");

            return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
            {
                CotXml = cotXml,
                UpdatesNodeState = true,
                DebugMessage = $"Position: {nodeState.DisplayName} @ {latitude:F6}, {longitude:F6}"
            });
        }
    }
}
