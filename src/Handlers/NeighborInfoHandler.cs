using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handler for NEIGHBORINFO_APP (portnum 71) packets.
    /// Deserializes neighbor info and stores neighbor list per node.
    /// </summary>
    public class NeighborInfoHandler : IPacketHandler
    {
        /// <inheritdoc />
        public PortNum HandledPortNum => PortNum.NeighborinfoApp;

        /// <inheritdoc />
        public Task<PacketHandlerResult?> HandleAsync(MeshPacket packet, PacketHandlerContext context)
        {
            if (packet.Decoded?.Payload == null || packet.Decoded.Payload.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("[NeighborInfoHandler] Empty payload, skipping");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            Meshtastic.Protobufs.NeighborInfo neighborInfo;
            try
            {
                neighborInfo = Meshtastic.Protobufs.NeighborInfo.Parser.ParseFrom(packet.Decoded.Payload);
            }
            catch (InvalidProtocolBufferException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NeighborInfoHandler] Warning: Failed to parse NeighborInfo: {ex.Message}");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            if (context.NodeStateManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[NeighborInfoHandler] Warning: No NodeStateManager in context");
                return Task.FromResult<PacketHandlerResult?>(null);
            }

            // Get or create the reporting node's state
            var reportingNodeId = neighborInfo.NodeId != 0 ? neighborInfo.NodeId : packet.From;
            var nodeState = context.NodeStateManager.GetOrCreate(context.ConnectionId, reportingNodeId);

            // Clear existing neighbors and rebuild from this packet
            nodeState.Neighbors.Clear();

            foreach (var neighbor in neighborInfo.Neighbors)
            {
                if (neighbor.NodeId == 0) continue; // Skip invalid entries

                // Try to get neighbor's display name from existing node state
                var neighborState = context.NodeStateManager.Get(context.ConnectionId, neighbor.NodeId);
                var neighborName = neighborState?.DisplayName;

                nodeState.Neighbors.Add(new Models.NeighborInfo
                {
                    NodeId = neighbor.NodeId,
                    NodeName = neighborName,
                    Snr = neighbor.Snr,
                    LastUpdate = DateTime.UtcNow
                });
            }

            nodeState.LastHeard = DateTime.UtcNow;
            context.NodeStateManager.Update(nodeState);

            System.Diagnostics.Debug.WriteLine(
                $"[NeighborInfoHandler] Node {nodeState.DisplayName} has {nodeState.Neighbors.Count} neighbors");

            return Task.FromResult<PacketHandlerResult?>(new PacketHandlerResult
            {
                UpdatesNodeState = true,
                DebugMessage = $"NeighborInfo: {nodeState.DisplayName} has {nodeState.Neighbors.Count} neighbors"
            });
        }
    }
}
