using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Prism.Commands;
using WinTak.Framework.Docking;
using WinTak.Framework.Docking.Attributes;
using WinTakMeshtasticPlugin.Plugin;
using WinTakMeshtasticPlugin.Connection;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// ViewModel for the Meshtastic dockable panel.
    /// Provides connection status, node list, chat, and settings UI.
    /// </summary>
    [DockPane(Id, "Meshtastic", Content = typeof(MeshtasticView))]
    [Export]
    public class MeshtasticDockPane : DockPane
    {
        public const string Id = "MeshtasticDockPane";

        private string _connectionStatus = "Disconnected";
        private string _hostname = "192.168.1.117";
        private int _port = 4403;
        private int _nodeCount = 0;
        private bool _isConnected = false;

        /// <summary>
        /// Gets the MeshtasticModule instance.
        /// </summary>
        private MeshtasticModule Module => MeshtasticModule.Instance;

        public MeshtasticDockPane()
        {
            ConnectCommand = new DelegateCommand(OnConnect, CanConnect);
            DisconnectCommand = new DelegateCommand(OnDisconnect, CanDisconnect);

            // Defer subscription until module is available
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(InitializeFromModule));
        }

        private void InitializeFromModule()
        {
            var module = Module;
            if (module != null)
            {
                // Load saved settings
                Hostname = module.Settings.Hostname ?? "192.168.1.117";
                Port = module.Settings.Port > 0 ? module.Settings.Port : 4403;

                // Subscribe to connection state changes
                module.ConnectionStateChanged += OnModuleConnectionStateChanged;

                // Initialize state from module
                UpdateConnectionState(module.ConnectionState);
            }
        }

        private void OnModuleConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            // Marshal to UI thread if needed
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                UpdateConnectionState(e.NewState);
                var module = Module;
                if (module != null)
                {
                    NodeCount = module.NodeCount;
                }
            });
        }

        #region Properties

        /// <summary>
        /// Current connection status text.
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        /// <summary>
        /// Meshtastic node hostname or IP address.
        /// </summary>
        public string Hostname
        {
            get => _hostname;
            set => SetProperty(ref _hostname, value);
        }

        /// <summary>
        /// Meshtastic node TCP port.
        /// </summary>
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        /// <summary>
        /// Number of tracked mesh nodes.
        /// </summary>
        public int NodeCount
        {
            get => _nodeCount;
            set => SetProperty(ref _nodeCount, value);
        }

        /// <summary>
        /// Whether currently connected to a Meshtastic node.
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    ((DelegateCommand)ConnectCommand).RaiseCanExecuteChanged();
                    ((DelegateCommand)DisconnectCommand).RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to connect to the Meshtastic node.
        /// </summary>
        public ICommand ConnectCommand { get; }

        /// <summary>
        /// Command to disconnect from the Meshtastic node.
        /// </summary>
        public ICommand DisconnectCommand { get; }

        private bool CanConnect()
        {
            return !IsConnected && !string.IsNullOrWhiteSpace(Hostname);
        }

        private void OnConnect()
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "wintak", "plugins", "WinTakMeshtasticPlugin", "load.log");
                System.IO.File.AppendAllText(logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - OnConnect called, Module={Module != null}\r\n");
            }
            catch { }

            var module = Module;
            if (module == null)
            {
                ConnectionStatus = "Error: Module not loaded";
                return;
            }

            ConnectionStatus = "Connecting...";
            module.ConnectAsync(Hostname, Port);
        }

        private bool CanDisconnect()
        {
            return IsConnected;
        }

        private void OnDisconnect()
        {
            var module = Module;
            if (module == null)
            {
                return;
            }

            ConnectionStatus = "Disconnecting...";
            System.Diagnostics.Debug.WriteLine("[MeshtasticDockPane] Disconnecting");
            module.DisconnectAsync();
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            NodeCount = 0;
        }

        #endregion

        /// <summary>
        /// Update connection state from external events.
        /// </summary>
        public void UpdateConnectionState(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connected:
                    IsConnected = true;
                    ConnectionStatus = $"Connected to {Hostname}:{Port}";
                    break;
                case ConnectionState.Disconnected:
                    IsConnected = false;
                    ConnectionStatus = "Disconnected";
                    break;
                case ConnectionState.Connecting:
                    ConnectionStatus = "Connecting...";
                    break;
                case ConnectionState.Reconnecting:
                    ConnectionStatus = "Reconnecting...";
                    break;
            }
        }
    }
}
