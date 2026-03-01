using System;
using System.Collections.Generic;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Logging;
using WinTakMeshtasticPlugin.Handlers;

namespace WinTakMeshtasticPlugin.Plugin
{
    /// <summary>
    /// Registry for mapping Meshtastic portnums to their packet handlers.
    /// Handlers are registered at plugin startup and looked up when packets arrive.
    /// </summary>
    public class HandlerRegistry : IHandlerRegistry
    {
        private readonly Dictionary<PortNum, IPacketHandler> _handlers = new();
        private readonly ILogger<HandlerRegistry>? _logger;

        public HandlerRegistry(ILogger<HandlerRegistry>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Register a packet handler for a specific portnum.
        /// </summary>
        /// <param name="handler">The handler to register.</param>
        /// <exception cref="ArgumentException">Thrown if a handler for this portnum already exists.</exception>
        public void Register(IPacketHandler handler)
        {
            if (_handlers.ContainsKey(handler.HandledPortNum))
            {
                throw new ArgumentException(
                    $"Handler for portnum {handler.HandledPortNum} already registered.",
                    nameof(handler));
            }

            _handlers[handler.HandledPortNum] = handler;
            _logger?.LogDebug("Registered handler for portnum {PortNum}", handler.HandledPortNum);
        }

        /// <summary>
        /// Get the handler for a specific portnum.
        /// </summary>
        /// <param name="portNum">The portnum to look up.</param>
        /// <returns>The registered handler, or null if none is registered.</returns>
        public IPacketHandler? GetHandler(PortNum portNum)
        {
            if (_handlers.TryGetValue(portNum, out var handler))
            {
                return handler;
            }

            // Log unknown portnums at Debug level per CLAUDE.md gotchas
            _logger?.LogDebug("No handler registered for portnum {PortNum}", portNum);
            return null;
        }

        /// <summary>
        /// Check if a handler is registered for a specific portnum.
        /// </summary>
        public bool HasHandler(PortNum portNum) => _handlers.ContainsKey(portNum);

        /// <summary>
        /// Get all registered portnums.
        /// </summary>
        public IEnumerable<PortNum> RegisteredPortNums => _handlers.Keys;

        /// <summary>
        /// Register all standard handlers for Phase 1.
        /// Call this during plugin initialization.
        /// </summary>
        public void RegisterDefaultHandlers()
        {
            // Phase 1 handlers (Must priority)
            Register(new PositionHandler());      // POSITION_APP (3)
            Register(new NodeInfoHandler());      // NODEINFO_APP (4)
            Register(new TextMessageHandler());   // TEXT_MESSAGE_APP (1)

            // Phase 1 handlers (Should priority)
            // Register(new MapReportHandler());     // MAP_REPORT_APP (73)
            // Register(new AtakPluginHandler());    // ATAK_PLUGIN (72)

            // Phase 2 handlers
            // Register(new TelemetryHandler());     // TELEMETRY_APP (67)
            // Register(new NeighborInfoHandler());  // NEIGHBORINFO_APP (71)

            // Phase 3 handlers
            // Register(new WaypointHandler());      // WAYPOINT_APP (8)
            // Register(new TracerouteHandler());    // TRACEROUTE_APP (70)
            // Register(new DetectionSensorHandler()); // DETECTION_SENSOR_APP (68)
            // Register(new AlertHandler());         // ALERT_APP (226)
            // Register(new PaxcounterHandler());    // PAXCOUNTER_APP (69)

            _logger?.LogInformation("Default handlers registered: POSITION_APP, NODEINFO_APP, TEXT_MESSAGE_APP");
        }
    }

    /// <summary>
    /// Interface for the handler registry to support dependency injection and testing.
    /// </summary>
    public interface IHandlerRegistry
    {
        void Register(IPacketHandler handler);
        IPacketHandler? GetHandler(PortNum portNum);
        bool HasHandler(PortNum portNum);
        IEnumerable<PortNum> RegisteredPortNums { get; }
        void RegisterDefaultHandlers();
    }
}
