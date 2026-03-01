using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WinTakMeshtasticPlugin.Connection;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// ViewModel for the settings panel.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly PluginSettings _settings;
        private readonly IChannelManager _channelManager;
        private ConnectionState _connectionState = ConnectionState.Disconnected;

        // Connection callbacks
        private readonly Action<string, int> _connectAction;
        private readonly Action _disconnectAction;
        private readonly Func<int> _getNodeCount;

        public SettingsViewModel(
            PluginSettings settings,
            IChannelManager channelManager,
            Action<string, int> connectAction,
            Action disconnectAction,
            Func<int> getNodeCount)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
            _connectAction = connectAction ?? throw new ArgumentNullException(nameof(connectAction));
            _disconnectAction = disconnectAction ?? throw new ArgumentNullException(nameof(disconnectAction));
            _getNodeCount = getNodeCount ?? throw new ArgumentNullException(nameof(getNodeCount));

            // Initialize commands
            ConnectCommand = new RelayCommand(ExecuteConnect, CanExecuteConnect);
            SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);

            // Initialize collections
            Channels = new ObservableCollection<ChannelViewModel>();
            TransmitChannels = new ObservableCollection<ChannelViewModel>();

            // Subscribe to channel changes
            _channelManager.ChannelChanged += OnChannelChanged;

            // Load initial channel data
            RefreshChannels();
        }

        #region Connection Properties

        public string Hostname
        {
            get => _settings.Hostname;
            set
            {
                if (_settings.Hostname != value)
                {
                    _settings.Hostname = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Port
        {
            get => _settings.Port;
            set
            {
                if (_settings.Port != value)
                {
                    _settings.Port = Math.Clamp(value, 1, 65535);
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoConnect
        {
            get => _settings.AutoConnect;
            set
            {
                if (_settings.AutoConnect != value)
                {
                    _settings.AutoConnect = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ReconnectIntervalSeconds
        {
            get => _settings.ReconnectIntervalSeconds;
            set
            {
                if (_settings.ReconnectIntervalSeconds != value)
                {
                    _settings.ReconnectIntervalSeconds = Math.Clamp(value, 5, 60);
                    OnPropertyChanged();
                }
            }
        }

        public ConnectionState ConnectionState
        {
            get => _connectionState;
            set
            {
                if (_connectionState != value)
                {
                    _connectionState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsNotConnected));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ConnectButtonText));
                    (ConnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsConnected => _connectionState == ConnectionState.Connected;
        public bool IsNotConnected => _connectionState == ConnectionState.Disconnected;

        public Brush StatusColor => _connectionState switch
        {
            ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            _ => new SolidColorBrush(Color.FromRgb(244, 67, 54))
        };

        public string StatusText => _connectionState switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Connecting => "Connecting...",
            ConnectionState.Reconnecting => "Reconnecting...",
            _ => "Disconnected"
        };

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        #endregion

        #region Channel Properties

        public ObservableCollection<ChannelViewModel> Channels { get; }
        public ObservableCollection<ChannelViewModel> TransmitChannels { get; }

        private ChannelViewModel? _selectedOutboundChannel;
        public ChannelViewModel? SelectedOutboundChannel
        {
            get => _selectedOutboundChannel;
            set
            {
                if (_selectedOutboundChannel != value)
                {
                    _selectedOutboundChannel = value;
                    if (value != null)
                    {
                        _channelManager.SelectedOutboundChannel = value.Index;
                        _settings.SelectedOutboundChannel = value.Index;
                    }
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Statistics

        public int NodeCount => _getNodeCount();
        public int ChannelCount => _channelManager.GetAllChannels().Count();

        public void RefreshStatistics()
        {
            OnPropertyChanged(nameof(NodeCount));
            OnPropertyChanged(nameof(ChannelCount));
        }

        #endregion

        #region Advanced Settings

        public int StaleNodeTimeoutHours
        {
            get => _settings.StaleNodeTimeoutHours;
            set
            {
                if (_settings.StaleNodeTimeoutHours != value)
                {
                    _settings.StaleNodeTimeoutHours = Math.Clamp(value, 1, 168);
                    OnPropertyChanged();
                }
            }
        }

        public bool TopologyOverlayEnabled
        {
            get => _settings.TopologyOverlayEnabled;
            set
            {
                if (_settings.TopologyOverlayEnabled != value)
                {
                    _settings.TopologyOverlayEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OutboundPliEnabled
        {
            get => _settings.OutboundPliEnabled;
            set
            {
                if (_settings.OutboundPliEnabled != value)
                {
                    _settings.OutboundPliEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OutboundPliVisibility));
                }
            }
        }

        public int OutboundPliIntervalSeconds
        {
            get => _settings.OutboundPliIntervalSeconds;
            set
            {
                if (_settings.OutboundPliIntervalSeconds != value)
                {
                    _settings.OutboundPliIntervalSeconds = Math.Clamp(value, 10, 600);
                    OnPropertyChanged();
                }
            }
        }

        public Visibility OutboundPliVisibility =>
            OutboundPliEnabled ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        private void ExecuteConnect()
        {
            if (IsConnected)
            {
                _disconnectAction();
            }
            else
            {
                _connectAction(Hostname, Port);
            }
        }

        private bool CanExecuteConnect()
        {
            return _connectionState != ConnectionState.Connecting &&
                   _connectionState != ConnectionState.Reconnecting;
        }

        private void ExecuteSaveSettings()
        {
            _settings.Validate();
            _settings.Save();
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Settings saved");
        }

        #endregion

        #region Channel Management

        private void OnChannelChanged(object? sender, ChannelChangedEventArgs e)
        {
            // Update UI on UI thread
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                RefreshChannels();
                RefreshStatistics();
            });
        }

        public void RefreshChannels()
        {
            Channels.Clear();
            TransmitChannels.Clear();

            foreach (var channel in _channelManager.GetAllChannels())
            {
                var vm = new ChannelViewModel(channel, _channelManager);
                Channels.Add(vm);

                if (channel.TransmitEnabled)
                {
                    TransmitChannels.Add(vm);
                }
            }

            // Select current outbound channel
            _selectedOutboundChannel = TransmitChannels.FirstOrDefault(
                c => c.Index == _settings.SelectedOutboundChannel);
            OnPropertyChanged(nameof(SelectedOutboundChannel));
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for a single channel in the list.
    /// </summary>
    public class ChannelViewModel : INotifyPropertyChanged
    {
        private readonly ChannelState _channel;
        private readonly IChannelManager _channelManager;

        public ChannelViewModel(ChannelState channel, IChannelManager channelManager)
        {
            _channel = channel;
            _channelManager = channelManager;
        }

        public int Index => _channel.Index;
        public string DisplayName => _channel.DisplayName;
        public bool IsAdmin => _channel.IsAdmin;
        public Visibility IsAdminVisibility => IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        public bool ReceiveEnabled
        {
            get => _channel.ReceiveEnabled;
            set
            {
                if (_channel.ReceiveEnabled != value)
                {
                    (_channelManager as ChannelManager)?.SetReceiveEnabled(Index, value);
                    OnPropertyChanged();
                }
            }
        }

        public Brush TeamColorBrush => GetTeamColorBrush(_channel.TeamColor);

        private static Brush GetTeamColorBrush(string teamColor)
        {
            return teamColor switch
            {
                "Cyan" => new SolidColorBrush(Color.FromRgb(0, 188, 212)),
                "Green" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                "Yellow" => new SolidColorBrush(Color.FromRgb(255, 235, 59)),
                "Orange" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                "Red" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                "Purple" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                "White" => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                "Magenta" => new SolidColorBrush(Color.FromRgb(233, 30, 99)),
                _ => new SolidColorBrush(Color.FromRgb(0, 188, 212))
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple relay command implementation.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
