using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Telemetry details window for a specific mesh node.
    /// </summary>
    public partial class TelemetryWindow : Window
    {
        public TelemetryWindow(NodeState nodeState)
        {
            InitializeComponent();
            DataContext = new TelemetryWindowViewModel(nodeState);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// ViewModel for the telemetry window. Wraps NodeState with display-friendly properties.
    /// </summary>
    public class TelemetryWindowViewModel : INotifyPropertyChanged
    {
        private readonly NodeState _nodeState;

        public TelemetryWindowViewModel(NodeState nodeState)
        {
            _nodeState = nodeState ?? throw new ArgumentNullException(nameof(nodeState));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // Node Identity
        public string LongName => _nodeState.LongName;
        public string ShortName => _nodeState.ShortName;
        public string HardwareModel => _nodeState.HardwareModel;
        public string FirmwareVersion => _nodeState.FirmwareVersion;
        public string NodeIdHex => _nodeState.NodeIdHex;
        public string LastHeardFormatted => FormatLastHeard(_nodeState.LastHeard);

        // Device Telemetry
        public string BatteryDisplay => _nodeState.DeviceTelemetry?.BatteryLevel.HasValue == true
            ? $"{_nodeState.DeviceTelemetry.BatteryLevel}%"
            : "No data";

        public string VoltageDisplay => _nodeState.DeviceTelemetry?.Voltage.HasValue == true
            ? $"{_nodeState.DeviceTelemetry.Voltage:F2}V"
            : "No data";

        public string UptimeDisplay => _nodeState.DeviceTelemetry?.UptimeSeconds.HasValue == true
            ? _nodeState.DeviceTelemetry.UptimeFormatted
            : "No data";

        public string ChannelUtilDisplay => _nodeState.DeviceTelemetry?.ChannelUtilization.HasValue == true
            ? $"{_nodeState.DeviceTelemetry.ChannelUtilization:F1}%"
            : "No data";

        public string AirUtilTxDisplay => _nodeState.DeviceTelemetry?.AirUtilTx.HasValue == true
            ? $"{_nodeState.DeviceTelemetry.AirUtilTx:F1}%"
            : "No data";

        // Environment Telemetry
        public string TemperatureDisplay
        {
            get
            {
                var env = _nodeState.EnvironmentTelemetry;
                if (env?.Temperature.HasValue != true)
                    return "No data";

                // Show both Celsius and Fahrenheit
                return $"{env.Temperature:F1}°C ({env.TemperatureFahrenheit:F1}°F)";
            }
        }

        public string HumidityDisplay => _nodeState.EnvironmentTelemetry?.RelativeHumidity.HasValue == true
            ? $"{_nodeState.EnvironmentTelemetry.RelativeHumidity:F1}%"
            : "No data";

        public string PressureDisplay
        {
            get
            {
                var env = _nodeState.EnvironmentTelemetry;
                if (env?.BarometricPressure.HasValue != true)
                    return "No data";

                // Show both hPa and inHg
                return $"{env.BarometricPressure:F1} hPa ({env.PressureInHg:F2} inHg)";
            }
        }

        public bool HasIaq => _nodeState.EnvironmentTelemetry?.Iaq.HasValue == true;

        // Neighbors
        public bool HasNeighbors => _nodeState.Neighbors?.Count > 0;

        public string NeighborCountDisplay => _nodeState.Neighbors?.Count > 0
            ? $"{_nodeState.Neighbors.Count} neighbor(s)"
            : "No neighbors";

        public IEnumerable<NeighborViewModel> Neighbors => _nodeState.Neighbors?
            .OrderByDescending(n => n.Snr)
            .Select(n => new NeighborViewModel(n))
            ?? Enumerable.Empty<NeighborViewModel>();

        public string IaqDisplay
        {
            get
            {
                var iaq = _nodeState.EnvironmentTelemetry?.Iaq;
                if (!iaq.HasValue)
                    return string.Empty;

                // IAQ scale: 0-50 Excellent, 51-100 Good, 101-150 Moderate, 151-200 Poor, 201-300 Bad, 301+ Very Bad
                string quality;
                if (iaq <= 50) quality = "Excellent";
                else if (iaq <= 100) quality = "Good";
                else if (iaq <= 150) quality = "Moderate";
                else if (iaq <= 200) quality = "Poor";
                else if (iaq <= 300) quality = "Bad";
                else quality = "Very Bad";

                return $"{iaq:F0} ({quality})";
            }
        }

        private string FormatLastHeard(DateTime lastHeard)
        {
            if (lastHeard == DateTime.MinValue)
                return "Never";

            var elapsed = DateTime.UtcNow - lastHeard;

            if (elapsed.TotalSeconds < 60)
                return $"{(int)elapsed.TotalSeconds}s ago";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m ago";

            return lastHeard.ToLocalTime().ToString("g");
        }
    }

    /// <summary>
    /// Converter for boolean to visibility.
    /// </summary>
    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// ViewModel for displaying a neighbor in the telemetry window.
    /// </summary>
    public class NeighborViewModel
    {
        private readonly NeighborInfo _neighbor;

        public NeighborViewModel(NeighborInfo neighbor)
        {
            _neighbor = neighbor ?? throw new ArgumentNullException(nameof(neighbor));
        }

        public string DisplayName => !string.IsNullOrEmpty(_neighbor.NodeName)
            ? _neighbor.NodeName
            : _neighbor.NodeIdHex;

        public float Snr => _neighbor.Snr;

        public string SnrDisplay => $"{_neighbor.Snr:F1} dB";
    }

    /// <summary>
    /// Converter for SNR value to color.
    /// Green >= -5 dB, Yellow >= -10 dB, Red otherwise.
    /// </summary>
    public class SnrToColorConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush YellowBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));

        static SnrToColorConverter()
        {
            GreenBrush.Freeze();
            YellowBrush.Freeze();
            RedBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is float snr)
            {
                if (snr >= -5) return GreenBrush;
                if (snr >= -10) return YellowBrush;
                return RedBrush;
            }
            return RedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
