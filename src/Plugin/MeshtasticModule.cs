using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Xml;
using Prism.Events;
using WinTakMeshtasticPlugin.Helpers;
using WinTak.Common.CoT;
using WinTak.Common.Services;
using WinTak.CursorOnTarget.Services;
using WinTak.Framework;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.CoT;
using WinTakMeshtasticPlugin.Handlers;
using WinTakMeshtasticPlugin.Messaging;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Plugin
{
    /// <summary>
    /// Main entry point for the Meshtastic WinTAK plugin.
    /// Uses MEF (Managed Extensibility Framework) for plugin composition.
    /// </summary>
    [Export(typeof(ITakModule))]
    public class MeshtasticModule : ITakModule
    {
        // Static constructor for early diagnostics
        static MeshtasticModule()
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "wintak", "plugins", "WinTakMeshtasticPlugin", "load.log");
                System.IO.File.AppendAllText(logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Static constructor called\r\n");
            }
            catch { }
        }

        /// <summary>
        /// Static instance for access from UI components.
        /// </summary>
        public static MeshtasticModule Instance { get; private set; }

        /// <summary>
        /// Event raised when connection state changes.
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICotMessageSender _cotMessageSender;
        private readonly ICotMessageReceiver _cotMessageReceiver;
        private readonly ILocationService _locationService;
        private readonly ICommunicationService _communicationService;

        private readonly IHandlerRegistry _handlerRegistry;
        private readonly ICotBuilder _cotBuilder;
        private readonly NodeStateManager _nodeStateManager;
        private readonly ChannelManager _channelManager;
        private readonly ChannelHandler _channelHandler;
        private readonly PluginSettings _settings;

        private MeshtasticTcpClient? _client;
        private OutboundMessageService? _outboundMessageService;
        private CancellationTokenSource? _cts;
        private TextMessageHandler? _textMessageHandler;

        /// <summary>
        /// Current connection state for UI binding.
        /// </summary>
        public ConnectionState ConnectionState => _client?.State ?? ConnectionState.Disconnected;

        /// <summary>
        /// The channel manager for tracking Meshtastic channel configuration.
        /// </summary>
        public IChannelManager ChannelManager => _channelManager;

        /// <summary>
        /// The plugin settings.
        /// </summary>
        public PluginSettings Settings => _settings;

        /// <summary>
        /// The outbound message service for sending messages to the mesh.
        /// </summary>
        public IOutboundMessageService? OutboundMessageService => _outboundMessageService;

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

            // Load settings from disk
            _settings = PluginSettings.Load();

            _handlerRegistry = new HandlerRegistry();
            _cotBuilder = new CotBuilder();
            _nodeStateManager = new NodeStateManager();
            _channelManager = new ChannelManager();
            _channelHandler = new ChannelHandler(_channelManager);

            // Apply saved channel receive settings
            foreach (var kvp in _settings.ChannelReceiveEnabled)
            {
                _channelManager.SetReceiveEnabled(kvp.Key, kvp.Value);
            }

            // Apply saved outbound channel selection
            _channelManager.SelectedOutboundChannel = _settings.SelectedOutboundChannel;
        }

        /// <summary>
        /// Called by WinTAK during module initialization phase.
        /// </summary>
        public void Initialize()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initializing...");
            Instance = this;
        }

        /// <summary>
        /// Called by WinTAK after all modules are initialized.
        /// Start background services and establish connections.
        /// </summary>
        public void Startup()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin starting up...");

            try
            {
                _cts = new CancellationTokenSource();

                // Register packet handlers for each supported portnum
                _handlerRegistry.RegisterDefaultHandlers();

                // Get reference to TextMessageHandler for event wiring
                _textMessageHandler = _handlerRegistry.GetHandler(Meshtastic.Protobufs.PortNum.TextMessageApp) as TextMessageHandler;
                if (_textMessageHandler != null)
                {
                    _textMessageHandler.MessageReceived += OnTextMessageReceived;
                }

                // Subscribe to channel changes to persist settings
                _channelManager.ChannelChanged += OnChannelChanged;

                // Subscribe to CoT messages to capture outbound operator PLI
                _cotMessageReceiver.MessageReceived += OnCotMessageReceived;

                // Subscribe to operator position changes for outbound PLI
                _locationService.PositionChanged += OnOperatorPositionChanged;

                // Subscribe to node state changes for logging
                _nodeStateManager.NodeStateChanged += OnNodeStateChanged;
                _nodeStateManager.NodeRemoved += OnNodeRemoved;

                // Auto-connect if enabled in settings
                if (_settings.AutoConnect && !string.IsNullOrEmpty(_settings.Hostname))
                {
                    System.Diagnostics.Debug.WriteLine($"[Meshtastic] Auto-connecting to {_settings.Hostname}:{_settings.Port}");
                    ConnectAsync(_settings.Hostname, _settings.Port);
                }

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Plugin initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handle text message received from mesh.
        /// Routes to channel-specific chat rooms (MSG-02).
        /// </summary>
        private void OnTextMessageReceived(object? sender, TextMessageReceivedEventArgs e)
        {
            // Check if this channel is enabled for receive (CHN-03)
            if (!(_channelManager as ChannelManager)?.IsReceiveEnabled(e.ChannelIndex) ?? false)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Meshtastic] Message dropped - channel {e.ChannelIndex} receive disabled");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[Meshtastic] Chat message from {e.SenderCallsign} to {e.ChatRoom}: {e.Message}");

            // CoT injection is handled by the TextMessageHandler result
            // This event is for additional processing like chat window updates
        }

        /// <summary>
        /// Handle channel configuration received from mesh.
        /// </summary>
        private void OnChannelReceived(object? sender, ChannelReceivedEventArgs e)
        {
            _channelHandler.HandleChannel(e.Channel);
        }

        /// <summary>
        /// Handle channel state changes for settings persistence.
        /// </summary>
        private void OnChannelChanged(object? sender, ChannelChangedEventArgs e)
        {
            // Persist channel receive enabled state
            _settings.ChannelReceiveEnabled[e.Channel.Index] = e.Channel.ReceiveEnabled;
            _settings.SelectedOutboundChannel = _channelManager.SelectedOutboundChannel;

            System.Diagnostics.Debug.WriteLine(
                $"[Meshtastic] Channel {e.Channel.Index} changed - persist settings");
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
                _client.ChannelReceived -= OnChannelReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
                _outboundMessageService = null;
            }

            // Clear channel state on new connection
            _channelManager.Clear();

            // Store current settings
            _settings.Hostname = hostname;
            _settings.Port = port;

            var config = new MeshtasticClientConfig
            {
                Hostname = hostname,
                Port = port,
                ReconnectIntervalSeconds = _settings.ReconnectIntervalSeconds
            };

            System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connecting to {hostname}:{port}...");

            _client = new MeshtasticTcpClient(config);
            _client.PacketReceived += OnPacketReceived;
            _client.ChannelReceived += OnChannelReceived;
            _client.StateChanged += OnConnectionStateChanged;

            // Create outbound message service
            _outboundMessageService = new OutboundMessageService(_client, _channelManager);
            _outboundMessageService.MessageSent += OnOutboundMessageSent;

            try
            {
                if (_cts != null)
                {
                    await _client.ConnectAsync(_cts.Token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle outbound message sent event (for local echo).
        /// </summary>
        private void OnOutboundMessageSent(object? sender, OutboundMessageSentEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Meshtastic] Sent message to channel {e.ChannelName}: {e.Message}");
        }

        /// <summary>
        /// Update connection settings.
        /// </summary>
        public void UpdateSettings(string hostname, int port, int reconnectIntervalSeconds, bool autoConnect)
        {
            _settings.Hostname = hostname;
            _settings.Port = port;
            _settings.ReconnectIntervalSeconds = MathExtensions.Clamp(reconnectIntervalSeconds, 5, 60);
            _settings.AutoConnect = autoConnect;
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
                _client.ChannelReceived -= OnChannelReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;

                if (_outboundMessageService != null)
                {
                    _outboundMessageService.MessageSent -= OnOutboundMessageSent;
                    _outboundMessageService = null;
                }

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Disconnected");
            }
        }

        /// <summary>
        /// Clean up resources when plugin is unloaded.
        /// Called by WinTAK when the application is shutting down.
        /// </summary>
        public void Terminate()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin terminating...");

            // Save settings before shutdown
            try
            {
                _settings.Validate();
                _settings.Save();
                System.Diagnostics.Debug.WriteLine("[Meshtastic] Settings saved");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Failed to save settings: {ex.Message}");
            }

            _cts?.Cancel();

            if (_client != null)
            {
                _client.PacketReceived -= OnPacketReceived;
                _client.ChannelReceived -= OnChannelReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
            }

            if (_outboundMessageService != null)
            {
                _outboundMessageService.MessageSent -= OnOutboundMessageSent;
                _outboundMessageService = null;
            }

            if (_textMessageHandler != null)
            {
                _textMessageHandler.MessageReceived -= OnTextMessageReceived;
            }

            _channelManager.ChannelChanged -= OnChannelChanged;
            _cotMessageReceiver.MessageReceived -= OnCotMessageReceived;
            _locationService.PositionChanged -= OnOperatorPositionChanged;
            _nodeStateManager.NodeStateChanged -= OnNodeStateChanged;
            _nodeStateManager.NodeRemoved -= OnNodeRemoved;

            _cts?.Dispose();

            Instance = null;
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

        /// <summary>
        /// Create a SettingsViewModel for the settings UI panel.
        /// </summary>
        public UI.SettingsViewModel CreateSettingsViewModel()
        {
            return new UI.SettingsViewModel(
                _settings,
                _channelManager,
                ConnectAsync,
                DisconnectAsync,
                () => NodeCount);
        }

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
                ChannelManager = _channelManager,
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

            // Forward to subscribers (UI)
            ConnectionStateChanged?.Invoke(this, e);
        }
    }
}
