using System.Windows;
using WinTakMeshtasticPlugin.Models;
using WinTakMeshtasticPlugin.Plugin;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Settings dialog window for the Meshtastic plugin.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly PluginSettings _settings;
        private readonly SettingsWindowViewModel _viewModel;
        private readonly DisplayNameMode _originalDisplayNameMode;
        private readonly bool _originalTopologyOverlayEnabled;

        /// <summary>
        /// Create a settings window with the given settings instance.
        /// </summary>
        /// <param name="settings">The plugin settings to edit (modified in place).</param>
        public SettingsWindow(PluginSettings settings)
        {
            InitializeComponent();

            _settings = settings;
            _originalDisplayNameMode = settings.DisplayNameMode;
            _originalTopologyOverlayEnabled = settings.TopologyOverlayEnabled;
            _viewModel = new SettingsWindowViewModel(settings);
            DataContext = _viewModel;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Validate and save settings
            _settings.Validate();
            _settings.Save();

            // If display name mode changed, update CoT markers
            if (_settings.DisplayNameMode != _originalDisplayNameMode)
            {
                MeshtasticModule.Instance?.SetDisplayNameMode(_settings.DisplayNameMode);
            }

            // If topology overlay setting changed, update map immediately
            if (_settings.TopologyOverlayEnabled != _originalTopologyOverlayEnabled)
            {
                MeshtasticModule.Instance?.SetTopologyOverlayEnabled(_settings.TopologyOverlayEnabled);
            }

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            // Reload settings to discard changes
            var reloaded = PluginSettings.Load();

            // Copy reloaded values back to the original instance
            _settings.Hostname = reloaded.Hostname;
            _settings.Port = reloaded.Port;
            _settings.AutoConnect = reloaded.AutoConnect;
            _settings.ReconnectIntervalSeconds = reloaded.ReconnectIntervalSeconds;
            _settings.StaleNodeTimeoutHours = reloaded.StaleNodeTimeoutHours;
            _settings.TopologyOverlayEnabled = reloaded.TopologyOverlayEnabled;
            _settings.OutboundPliEnabled = reloaded.OutboundPliEnabled;
            _settings.OutboundPliIntervalSeconds = reloaded.OutboundPliIntervalSeconds;
            _settings.DisplayNameMode = reloaded.DisplayNameMode;

            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// ViewModel for the settings window. Binds directly to PluginSettings.
    /// </summary>
    public class SettingsWindowViewModel
    {
        private readonly PluginSettings _settings;

        public SettingsWindowViewModel(PluginSettings settings)
        {
            _settings = settings;
        }

        public string Hostname
        {
            get => _settings.Hostname;
            set => _settings.Hostname = value;
        }

        public int Port
        {
            get => _settings.Port;
            set => _settings.Port = value;
        }

        public bool AutoConnect
        {
            get => _settings.AutoConnect;
            set => _settings.AutoConnect = value;
        }

        public int ReconnectIntervalSeconds
        {
            get => _settings.ReconnectIntervalSeconds;
            set => _settings.ReconnectIntervalSeconds = value;
        }

        public int StaleNodeTimeoutHours
        {
            get => _settings.StaleNodeTimeoutHours;
            set => _settings.StaleNodeTimeoutHours = value;
        }

        public bool TopologyOverlayEnabled
        {
            get => _settings.TopologyOverlayEnabled;
            set => _settings.TopologyOverlayEnabled = value;
        }

        public bool UseShortName
        {
            get => _settings.DisplayNameMode == DisplayNameMode.ShortName;
            set
            {
                if (value)
                    _settings.DisplayNameMode = DisplayNameMode.ShortName;
            }
        }

        public bool UseLongName
        {
            get => _settings.DisplayNameMode == DisplayNameMode.LongName;
            set
            {
                if (value)
                    _settings.DisplayNameMode = DisplayNameMode.LongName;
            }
        }
    }
}
