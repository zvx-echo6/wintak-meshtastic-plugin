using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.Practices.Prism.Commands;
using WinTak.Framework.Docking;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// ViewModel for the Meshtastic dockable panel.
    /// Provides connection status, node list, chat, and settings UI.
    /// </summary>
    [DockPane(
        Id,
        typeof(MeshtasticView),
        Caption = "Meshtastic",
        StartupMode = DockPaneStartupMode.Unpinned,
        StartupState = DockPaneState.DockedLeft)]
    [Export]
    public class MeshtasticDockPane : DockPane
    {
        public const string Id = "WinTakMeshtasticPlugin.MeshtasticDockPane";

        private string _connectionStatus = "Disconnected";
        private string _hostname = "192.168.1.1";
        private int _port = 4403;
        private int _nodeCount = 0;
        private bool _isConnected = false;

        public MeshtasticDockPane()
        {
            ConnectCommand = new DelegateCommand(OnConnect, CanConnect);
            DisconnectCommand = new DelegateCommand(OnDisconnect, CanDisconnect);
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
            ConnectionStatus = "Connecting...";
            // TODO: Call MeshtasticModule.ConnectAsync(Hostname, Port)
            // For now, simulate connection
            IsConnected = true;
            ConnectionStatus = $"Connected to {Hostname}:{Port}";
        }

        private bool CanDisconnect()
        {
            return IsConnected;
        }

        private void OnDisconnect()
        {
            ConnectionStatus = "Disconnecting...";
            // TODO: Call MeshtasticModule.DisconnectAsync()
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            NodeCount = 0;
        }

        #endregion

        /// <summary>
        /// Update connection state from external events.
        /// </summary>
        public void UpdateConnectionState(Connection.ConnectionState state)
        {
            switch (state)
            {
                case Connection.ConnectionState.Connected:
                    IsConnected = true;
                    ConnectionStatus = $"Connected to {Hostname}:{Port}";
                    break;
                case Connection.ConnectionState.Disconnected:
                    IsConnected = false;
                    ConnectionStatus = "Disconnected";
                    break;
                case Connection.ConnectionState.Connecting:
                    ConnectionStatus = "Connecting...";
                    break;
                case Connection.ConnectionState.Reconnecting:
                    ConnectionStatus = "Reconnecting...";
                    break;
            }
        }
    }
}
