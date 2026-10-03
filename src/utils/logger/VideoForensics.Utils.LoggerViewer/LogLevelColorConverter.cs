using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoForensics.Utils.LoggerViewer
{
    /// <summary>
    /// Converts a log level string to a WPF Color for display.
    /// </summary>
    public class LogLevelColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string logLevel)
            {
                return Colors.Black;
            }

            return logLevel.ToLowerInvariant() switch
            {
                "debug" => Colors.Gray,
                "information" or "info" => Colors.Black,
                "warning" or "warn" => Color.FromRgb(255, 165, 0), // Orange
                "error" => Colors.Red,
                "critical" or "fatal" => Color.FromRgb(139, 0, 0), // DarkRed
                _ => Colors.Black
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
