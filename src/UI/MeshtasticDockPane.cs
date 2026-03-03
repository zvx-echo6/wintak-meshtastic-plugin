using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using WinTak.Framework.Docking;
using WinTak.Framework.Docking.Attributes;
using WinTakMeshtasticPlugin.Plugin;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// ViewModel for the Meshtastic dockable panel.
    /// Provides connection status, node list, and settings access.
    /// </summary>
    [DockPane(Id, "Meshtastic", Content = typeof(MeshtasticView))]
    [Export]
    public class MeshtasticDockPane : DockPane
    {
        public const string Id = "MeshtasticDockPane";

        private string _connectionStatus = "Disconnected";
        private string _hostname = "localhost";
        private int _port = 4403;
        private int _nodeCount = 0;
        private bool _isConnected = false;
        private ObservableCollection<NodeState> _nodes = new ObservableCollection<NodeState>();
        private NodeState _selectedNode;
        private System.Windows.Threading.DispatcherTimer _refreshTimer;
        private bool _topologyEnabled;

        /// <summary>
        /// Observable collection of mesh nodes for UI binding.
        /// </summary>
        public ObservableCollection<NodeState> Nodes
        {
            get => _nodes;
            set => SetProperty(ref _nodes, value);
        }

        /// <summary>
        /// Gets the MeshtasticModule instance.
        /// </summary>
        private MeshtasticModule Module => MeshtasticModule.Instance;

        public MeshtasticDockPane()
        {
            ConnectCommand = new DelegateCommand(OnConnect, CanConnect);
            DisconnectCommand = new DelegateCommand(OnDisconnect, CanDisconnect);
            OpenSettingsCommand = new DelegateCommand(OnOpenSettings);
            ToggleTopologyCommand = new DelegateCommand(OnToggleTopology);

            // Defer initialization until module is available
            Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(InitializeFromModule));
        }

        private void InitializeFromModule()
        {
            // Load settings directly from disk - don't depend on MeshtasticModule.Instance
            // which may not be set yet due to MEF initialization order
            var settings = PluginSettings.Load();
            Hostname = !string.IsNullOrWhiteSpace(settings.Hostname) ? settings.Hostname : "localhost";
            Port = settings.Port > 0 ? settings.Port : 4403;
            TopologyEnabled = settings.TopologyOverlayEnabled;

            // Subscribe to module events if available
            var module = Module;
            if (module != null)
            {
                module.ConnectionStateChanged += OnModuleConnectionStateChanged;
                UpdateConnectionState(module.ConnectionState);

                // If already connected (e.g., auto-connect completed before we subscribed),
                // start the refresh timer
                if (module.ConnectionState == ConnectionState.Connected)
                {
                    StartRefreshTimer();
                    RefreshNodes();
                }
            }
            else
            {
                // Module not available yet - retry after a short delay
                // This handles the case where MEF initialization order causes
                // the DockPane to initialize before MeshtasticModule.Instance is set
                Application.Current?.Dispatcher?.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(RetrySubscribeToModule));
            }
        }

        private void RetrySubscribeToModule()
        {
            var module = Module;
            if (module != null)
            {
                module.ConnectionStateChanged += OnModuleConnectionStateChanged;
                UpdateConnectionState(module.ConnectionState);

                // If already connected, start refresh timer
                if (module.ConnectionState == ConnectionState.Connected)
                {
                    StartRefreshTimer();
                    RefreshNodes();
                }
            }
        }

        private void OnModuleConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            // Marshal to UI thread if needed
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                UpdateConnectionState(e.NewState);
                RefreshNodes();

                // Start/stop refresh timer based on connection state
                if (e.NewState == ConnectionState.Connected)
                {
                    StartRefreshTimer();
                }
                else if (e.NewState == ConnectionState.Disconnected)
                {
                    StopRefreshTimer();
                }
            });
        }

        private void StartRefreshTimer()
        {
            if (_refreshTimer != null) return;

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (s, e) => RefreshNodes();
            _refreshTimer.Start();

            // Do an immediate refresh after a short delay to catch config dump
            Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    System.Threading.Thread.Sleep(2000);
                    RefreshNodes();
                }));
        }

        private void StopRefreshTimer()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        /// <summary>
        /// Refresh the nodes collection from the module.
        /// </summary>
        public void RefreshNodes()
        {
            var module = Module;
            if (module == null) return;

            NodeCount = module.NodeCount;

            // Update the observable collection
            var currentNodes = module.GetNodes().OrderByDescending(n => n.LastHeard).ToList();

            Nodes.Clear();
            foreach (var node in currentNodes)
            {
                Nodes.Add(node);
            }
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

        /// <summary>
        /// Currently selected node in the node list.
        /// </summary>
        public NodeState SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        /// <summary>
        /// Whether the global topology overlay is enabled.
        /// </summary>
        public bool TopologyEnabled
        {
            get => _topologyEnabled;
            set => SetProperty(ref _topologyEnabled, value);
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

        /// <summary>
        /// Command to open the settings dialog.
        /// </summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Command to toggle the global topology overlay.
        /// </summary>
        public ICommand ToggleTopologyCommand { get; }

        private bool CanConnect()
        {
            return !IsConnected && !string.IsNullOrWhiteSpace(Hostname);
        }

        private void OnConnect()
        {
            var module = Module;
            if (module == null)
            {
                ConnectionStatus = "Error: Module not loaded";
                return;
            }

            ConnectionStatus = "Connecting...";

            // Save the hostname/port to settings on every connect attempt
            // so auto-connect will use the last attempted connection
            module.Settings.Hostname = Hostname;
            module.Settings.Port = Port;
            module.Settings.Save();

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
            module.DisconnectAsync();
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            NodeCount = 0;
        }

        private void OnOpenSettings()
        {
            var module = Module;
            if (module == null)
            {
                MessageBox.Show("Plugin not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Open settings dialog with the actual settings instance
            var settingsWindow = new SettingsWindow(module.Settings)
            {
                Owner = Application.Current?.MainWindow
            };

            if (settingsWindow.ShowDialog() == true)
            {
                // Settings were saved - update UI with new values
                Hostname = module.Settings.Hostname;
                Port = module.Settings.Port;
                TopologyEnabled = module.Settings.TopologyOverlayEnabled;
            }
        }

        private void OnToggleTopology()
        {
            var module = Module;
            if (module == null) return;

            // Toggle the state
            TopologyEnabled = !TopologyEnabled;

            // Update module and save to settings
            module.SetTopologyOverlayEnabled(TopologyEnabled);
            module.Settings.Save();
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
