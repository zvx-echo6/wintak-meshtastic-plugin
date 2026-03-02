using System.Windows;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Settings dialog window for the Meshtastic plugin.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly PluginSettings _settings;
        private readonly SettingsWindowViewModel _viewModel;

        /// <summary>
        /// Create a settings window with the given settings instance.
        /// </summary>
        /// <param name="settings">The plugin settings to edit (modified in place).</param>
        public SettingsWindow(PluginSettings settings)
        {
            InitializeComponent();

            _settings = settings;
            _viewModel = new SettingsWindowViewModel(settings);
            DataContext = _viewModel;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Validate and save settings
            _settings.Validate();
            _settings.Save();

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
    }
}
