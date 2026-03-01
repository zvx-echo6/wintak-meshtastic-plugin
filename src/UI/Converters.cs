using System;
using System.Globalization;
using System.Windows.Data;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Inverts a boolean value for XAML binding.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }

    /// <summary>
    /// Converts ConnectionState enum to display color.
    /// </summary>
    public class ConnectionStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Connection.ConnectionState state)
            {
                return state switch
                {
                    Connection.ConnectionState.Connected => "#22AA22",
                    Connection.ConnectionState.Connecting => "#AAAA22",
                    Connection.ConnectionState.Reconnecting => "#AAAA22",
                    Connection.ConnectionState.Disconnected => "#AA2222",
                    _ => "#888888"
                };
            }
            return "#888888";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
