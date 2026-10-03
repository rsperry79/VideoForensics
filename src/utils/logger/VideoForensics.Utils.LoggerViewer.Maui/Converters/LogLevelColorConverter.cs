using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace VideoForensics.Utils.LoggerViewer.Maui.Converters
{
    /// <summary>
    /// Converts log level strings to appropriate colors.
    /// </summary>
    public class LogLevelColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is not string level)
                return Colors.Black;

            return level.ToLower() switch
            {
                "debug" => Colors.Gray,
                "information" => Colors.Black,
                "information (2)" => Colors.Black,
                "info" => Colors.Black,
                "warning" => Color.FromArgb("#FF8C00"), // Orange
                "error" => Colors.Red,
                "critical" => Color.FromArgb("#8B0000"), // Dark Red
                "fatal" => Color.FromArgb("#8B0000"), // Dark Red
                _ => Colors.Black
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts log level strings to appropriate text colors (for dark backgrounds).
    /// </summary>
    public class LogLevelTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is not string level)
                return Colors.White;

            return level.ToLower() switch
            {
                "debug" => Colors.LightGray,
                "information" => Colors.White,
                "information (2)" => Colors.White,
                "info" => Colors.White,
                "warning" => Color.FromArgb("#FFD700"), // Gold
                "error" => Color.FromArgb("#FF6B6B"), // Light Red
                "critical" => Color.FromArgb("#FF4444"), // Bright Red
                "fatal" => Color.FromArgb("#FF4444"), // Bright Red
                _ => Colors.White
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }
}
