using System;
using System.ComponentModel.Composition;
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
        private MeshtasticTcpClient _client;

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
                // Register packet handlers for each supported portnum
                _handlerRegistry.RegisterDefaultHandlers();

                // Subscribe to CoT messages to capture outbound operator PLI
                _cotMessageReceiver.MessageReceived += OnCotMessageReceived;

                // Subscribe to operator position changes for outbound PLI
                _locationService.PositionChanged += OnOperatorPositionChanged;

                // Test CoT injection with a synthetic marker
                InjectTestMarker();

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Plugin initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Inject a test marker to verify CoT API integration.
        /// Creates a marker at lat 42.75, lon -114.46 with callsign "MESH-TEST".
        /// </summary>
        private void InjectTestMarker()
        {
            System.Diagnostics.Debug.WriteLine("[Meshtastic] Injecting test CoT marker...");

            try
            {
                // Create a test node state
                var testNode = new NodeState
                {
                    ConnectionId = "TEST",
                    NodeId = 0xDEADBEEF,
                    ShortName = "MESH-TEST",
                    LongName = "Meshtastic Test Node",
                    Latitude = 42.75,
                    Longitude = -114.46,
                    Altitude = 1200,
                    Role = DeviceRole.Client,
                    LastHeard = DateTime.UtcNow,
                    LastPositionUpdate = DateTime.UtcNow
                };
                testNode.ChannelsMembership.Add(0); // Channel 0 = Cyan

                // Build CoT XML using our builder
                string cotXml = _cotBuilder.BuildNodePli(testNode);
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Generated CoT XML:\n{cotXml}");

                // Inject into WinTAK via ICotMessageSender
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cotXml);
                _cotMessageSender.Send(xmlDoc);

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Test marker injected successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Test marker injection failed: {ex.Message}");
            }
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
        public async void ConnectAsync(string hostname, int port = 4403)
        {
            if (_client != null)
            {
                await _client.DisconnectAsync();
                _client.Dispose();
            }

            var config = new MeshtasticClientConfig
            {
                Hostname = hostname,
                Port = port
            };

            _client = new MeshtasticTcpClient(config);
            _client.PacketReceived += OnPacketReceived;
            _client.StateChanged += OnConnectionStateChanged;

            await _client.ConnectAsync();
        }

        /// <summary>
        /// Disconnect from the current Meshtastic node.
        /// </summary>
        public async void DisconnectAsync()
        {
            if (_client != null)
            {
                await _client.DisconnectAsync();
                _client.PacketReceived -= OnPacketReceived;
                _client.StateChanged -= OnConnectionStateChanged;
                _client.Dispose();
                _client = null;
            }
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
