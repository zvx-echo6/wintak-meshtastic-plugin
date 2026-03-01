using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Xml;
using Microsoft.Practices.Prism.MefExtensions.Modularity;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.PubSubEvents;
using WinTak.Common.CoT;
using WinTak.Common.Services;
using WinTak.CursorOnTarget.Services;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.CoT;
using WinTakMeshtasticPlugin.Handlers;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Plugin
{
    /// <summary>
    /// Main entry point for the Meshtastic WinTAK plugin.
    /// Uses MEF (Managed Extensibility Framework) for plugin composition.
    /// </summary>
    [ModuleExport(typeof(MeshtasticModule), InitializationMode = InitializationMode.WhenAvailable)]
    public class MeshtasticModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ICotMessageSender _cotMessageSender;
        private readonly ICotMessageReceiver _cotMessageReceiver;
        private readonly ILocationService _locationService;
        private readonly ICommunicationService _communicationService;

        private readonly IHandlerRegistry _handlerRegistry;
        private readonly ICotBuilder _cotBuilder;
        private readonly NodeStateManager _nodeStateManager;
        private MeshtasticTcpClient _client;
        private CancellationTokenSource _cts;

        // Default connection settings (will be configurable via settings panel later)
        private string _hostname = "localhost";
        private int _port = 4403;
        private int _reconnectIntervalSeconds = 15;
        private bool _autoConnect = false;

        /// <summary>
        /// Current connection state for UI binding.
        /// </summary>
        public ConnectionState ConnectionState => _client?.State ?? ConnectionState.Disconnected;

        /// <summary>
        /// MEF constructor with dependency injection.
        /// WinTAK provides all services via MEF composition.
        /// </summary>
        [ImportingConstructor]
        public MeshtasticModule(
            IEventAggregator eventAggregator,
            ICotMessageSender cotMessageSender,
            ICotMessageReceiver cotMessageReceiver,
            ILocationService locationService,
            ICommunicationService communicationService)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _cotMessageSender = cotMessageSender ?? throw new ArgumentNullException(nameof(cotMessageSender));
            _cotMessageReceiver = cotMessageReceiver ?? throw new ArgumentNullException(nameof(cotMessageReceiver));
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));

            _handlerRegistry = new HandlerRegistry();
            _cotBuilder = new CotBuilder();
            _nodeStateManager = new NodeStateManager();
        }

        /// <summary>
        /// Called by WinTAK during application startup.
        /// Initialize plugin services, register handlers, load settings.
        /// </summary>
        public void Initialize()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initializing...");

            try
            {
                _cts = new CancellationTokenSource();

                // Register packet handlers for each supported portnum
                _handlerRegistry.RegisterDefaultHandlers();

                // Subscribe to CoT messages to capture outbound operator PLI
                _cotMessageReceiver.MessageReceived += OnCotMessageReceived;

                // Subscribe to operator position changes for outbound PLI
                _locationService.PositionChanged += OnOperatorPositionChanged;

                // Subscribe to node state changes for logging
                _nodeStateManager.NodeStateChanged += OnNodeStateChanged;
                _nodeStateManager.NodeRemoved += OnNodeRemoved;

                // TODO: Load settings from JSON file
                // LoadSettings();

                // Auto-connect if enabled in settings
                if (_autoConnect && !string.IsNullOrEmpty(_hostname))
                {
                    System.Diagnostics.Debug.WriteLine($"[Meshtastic] Auto-connecting to {_hostname}:{_port}");
                    ConnectAsync(_hostname, _port);
                }

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Plugin initialization failed: {ex.Message}");
                throw;
            }
        }

        private void OnNodeStateChanged(object sender, NodeStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Meshtastic] Node state updated: {e.Node.DisplayName} @ {e.Node.Latitude?.ToString("F6") ?? "?"}, {e.Node.Longitude?.ToString("F6") ?? "?"}");
        }

        private void OnNodeRemoved(object sender, NodeStateRemovedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Meshtastic] Node removed: {e.Node.DisplayName}");
        }

        /// <summary>
        /// Inject a CoT event from a mesh node position.
        /// </summary>
        /// <param name="nodeState">Node with position data.</param>
        public void InjectNodePosition(NodeState nodeState)
        {
            if (nodeState == null || !nodeState.Latitude.HasValue || !nodeState.Longitude.HasValue)
            {
                return;
            }

            try
            {
                string cotXml = _cotBuilder.BuildNodePli(nodeState);
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cotXml);
                _cotMessageSender.Send(xmlDoc);

                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Injected position for {nodeState.DisplayName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Failed to inject position: {ex.Message}");
            }
        }

        /// <summary>
        /// Inject a GeoChat message from the mesh.
        /// </summary>
        public void InjectChatMessage(string senderUid, string senderCallsign, string message, string chatRoom)
        {
            try
            {
                string cotXml = _cotBuilder.BuildGeoChat(senderUid, senderCallsign, message, chatRoom);
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cotXml);
                _cotMessageSender.Send(xmlDoc);

                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Injected chat from {senderCallsign}: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Failed to inject chat: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcast a CoT message to all network contacts (for outbound PLI).
        /// Uses ICommunicationService for network transmission.
        /// </summary>
        public void BroadcastToNetwork(string cotXml)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cotXml);
                _communicationService.BroadcastCot(xmlDoc);

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Broadcast CoT to network");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Network broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle incoming CoT messages from WinTAK.
        /// Used to capture operator's own position for outbound mesh PLI.
        /// </summary>
        private void OnCotMessageReceived(object sender, CoTMessageArgument args)
        {
            // Log all received CoT for debugging
            System.Diagnostics.Debug.WriteLine($"[Meshtastic] CoT received: type={args.Type}, uid={args.CotEvent?.Uid}");

            // TODO: Filter for self-marker updates to relay to mesh
            // if (args.Type.StartsWith("a-f-") && args.CotEvent?.Uid == selfUid)
            // {
            //     RelayPositionToMesh(args.CotEvent);
            // }
        }

        /// <summary>
        /// Handle operator position changes for outbound PLI.
        /// </summary>
        private void OnOperatorPositionChanged(object sender, EventArgs e)
        {
            // Get current operator position
            var position = _locationService.GetGpsPosition();
            var selfEvent = _locationService.GetSelfCotEvent();

            System.Diagnostics.Debug.WriteLine(
                $"[Meshtastic] Operator position changed: {position?.Latitude}, {position?.Longitude}");

            // TODO: If outbound PLI is enabled, send to mesh
            // RelayPositionToMesh(selfEvent);
        }

        /// <summary>
        /// Connect to a Meshtastic node.
        /// Called from UI or on plugin startup if auto-connect is enabled.
        /// </summary>
        /// <param name="hostname">Hostname or IP address of the Meshtastic node.</param>
        /// <param name="port">TCP port (default 4403, must be configurable per requirements).</param>
        public async void ConnectAsync(string hostname, int port)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Cannot connect: hostname is required");
                return;
            }

            // Disconnect existing connection if any
            if (_client != null)
            {
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Disconnecting existing connection...");
                await _client.DisconnectAsync();
                _client.PacketReceived -= OnPacketReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
            }

            // Store current settings
            _hostname = hostname;
            _port = port;

            var config = new MeshtasticClientConfig
            {
                Hostname = hostname,
                Port = port,
                ReconnectIntervalSeconds = _reconnectIntervalSeconds
            };

            System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connecting to {hostname}:{port}...");

            _client = new MeshtasticTcpClient(config);
            _client.PacketReceived += OnPacketReceived;
            _client.StateChanged += OnConnectionStateChanged;

            try
            {
                await _client.ConnectAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update connection settings.
        /// </summary>
        public void UpdateSettings(string hostname, int port, int reconnectIntervalSeconds, bool autoConnect)
        {
            _hostname = hostname;
            _port = port;
            _reconnectIntervalSeconds = Math.Clamp(reconnectIntervalSeconds, 5, 60);
            _autoConnect = autoConnect;
        }

        /// <summary>
        /// Disconnect from the current Meshtastic node.
        /// </summary>
        public async void DisconnectAsync()
        {
            if (_client != null)
            {
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Disconnecting...");
                await _client.DisconnectAsync();
                _client.PacketReceived -= OnPacketReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Disconnected");
            }
        }

        /// <summary>
        /// Clean up resources when plugin is unloaded.
        /// </summary>
        public void Shutdown()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin shutting down...");

            _cts?.Cancel();

            if (_client != null)
            {
                _client.PacketReceived -= OnPacketReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
            }

            _cotMessageReceiver.MessageReceived -= OnCotMessageReceived;
            _locationService.PositionChanged -= OnOperatorPositionChanged;
            _nodeStateManager.NodeStateChanged -= OnNodeStateChanged;
            _nodeStateManager.NodeRemoved -= OnNodeRemoved;

            _cts?.Dispose();

            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin shutdown complete");
        }

        /// <summary>
        /// Get all tracked mesh nodes.
        /// </summary>
        public System.Collections.Generic.IEnumerable<NodeState> GetNodes()
        {
            return _nodeStateManager.GetAll();
        }

        /// <summary>
        /// Get the count of tracked nodes.
        /// </summary>
        public int NodeCount => _nodeStateManager.Count;

        private async void OnPacketReceived(object sender, MeshPacketReceivedEventArgs e)
        {
            // Get the handler for this packet's portnum
            var decoded = e.Packet.Decoded;
            if (decoded == null)
            {
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Received encrypted packet (no handler)");
                return;
            }

            var handler = _handlerRegistry.GetHandler(decoded.Portnum);
            if (handler == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] No handler for portnum {decoded.Portnum}");
                return;
            }

            // Process packet through handler and inject CoT
            var context = new PacketHandlerContext
            {
                ConnectionId = e.ConnectionId,
                NodeStateManager = _nodeStateManager,
                CotBuilder = _cotBuilder
            };

            try
            {
                var result = await handler.HandleAsync(e.Packet, context);
                if (result?.CotXml != null)
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(result.CotXml);
                    _cotMessageSender.Send(xmlDoc);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Handler error: {ex.Message}");
            }
        }

        private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connection state: {e.OldState} -> {e.NewState}");

            // TODO: Publish connection state change event via IEventAggregator
            // _eventAggregator.GetEvent<MeshtasticConnectionStateEvent>().Publish(e.NewState);
        }
    }
}
