using System;
using System.ComponentModel.Composition;
using Microsoft.Practices.Prism.MefExtensions.Modularity;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.PubSubEvents;
using WinTakMeshtasticPlugin.Connection;

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
        private readonly IHandlerRegistry _handlerRegistry;
        private MeshtasticTcpClient _client;

        /// <summary>
        /// MEF constructor with dependency injection.
        /// WinTAK provides IEventAggregator for pub/sub messaging.
        /// </summary>
        [ImportingConstructor]
        public MeshtasticModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _handlerRegistry = new HandlerRegistry();
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

                // TODO: Load connection settings from WinTAK preferences
                // TODO: Initialize CotDispatcher integration
                // TODO: Auto-connect if configured

                System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Meshtastic] Plugin initialization failed: {ex.Message}");
                throw;
            }
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

        private void OnPacketReceived(object sender, MeshPacketReceivedEventArgs e)
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

            // TODO: Process packet through handler and inject CoT
            // var context = new PacketHandlerContext { ConnectionId = e.ConnectionId, ... };
            // var result = await handler.HandleAsync(e.Packet, context);
            // if (result?.CotXml != null) CotDispatcher.Dispatch(result.CotXml);
        }

        private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Meshtastic] Connection state: {e.OldState} -> {e.NewState}");

            // TODO: Publish connection state change event via IEventAggregator
            // _eventAggregator.GetEvent<MeshtasticConnectionStateEvent>().Publish(e.NewState);
        }
    }
}
