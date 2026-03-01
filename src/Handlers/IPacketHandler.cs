using System.Threading.Tasks;
using Meshtastic.Protobufs;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Interface for handling specific Meshtastic portnum packet types.
    /// Each portnum (POSITION_APP, TEXT_MESSAGE_APP, TELEMETRY_APP, etc.)
    /// should have its own handler implementation.
    /// </summary>
    public interface IPacketHandler
    {
        /// <summary>
        /// The Meshtastic portnum this handler processes.
        /// </summary>
        PortNum HandledPortNum { get; }

        /// <summary>
        /// Process an incoming mesh packet and optionally generate CoT output.
        /// </summary>
        /// <param name="packet">The decoded MeshPacket from the Meshtastic node.</param>
        /// <param name="context">Context containing connection info and services.</param>
        /// <returns>
        /// A PacketHandlerResult containing any CoT XML to inject,
        /// or null if no CoT should be generated.
        /// </returns>
        Task<PacketHandlerResult?> HandleAsync(MeshPacket packet, PacketHandlerContext context);
    }

    /// <summary>
    /// Result of processing a mesh packet.
    /// </summary>
    public class PacketHandlerResult
    {
        /// <summary>
        /// CoT XML string to inject into WinTAK, or null if no CoT should be generated.
        /// </summary>
        public string? CotXml { get; set; }

        /// <summary>
        /// Indicates if this result should update node state (position, telemetry, etc.)
        /// </summary>
        public bool UpdatesNodeState { get; set; }

        /// <summary>
        /// Optional log message for debugging.
        /// </summary>
        public string? DebugMessage { get; set; }
    }

    /// <summary>
    /// Context passed to packet handlers with connection info and shared services.
    /// </summary>
    public class PacketHandlerContext
    {
        /// <summary>
        /// Unique identifier for the Meshtastic connection this packet came from.
        /// Used for multi-node support: node state is keyed by (connectionId, nodeId).
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// The node state manager for tracking mesh node information.
        /// </summary>
        public Models.INodeStateManager? NodeStateManager { get; set; }

        /// <summary>
        /// The channel manager for tracking channel configuration.
        /// </summary>
        public Models.IChannelManager? ChannelManager { get; set; }

        /// <summary>
        /// The CoT builder for generating Cursor-on-Target XML.
        /// </summary>
        public CoT.ICotBuilder? CotBuilder { get; set; }
    }
}
