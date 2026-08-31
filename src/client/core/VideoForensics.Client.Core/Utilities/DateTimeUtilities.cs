using System.Globalization;

namespace VideoForensics.Client.Core.Utilities
{
    /// <summary>Helper methods for parsing and formatting date/time strings.</summary>
    public static class DateTimeUtilities
    {
        private static readonly string[] SupportedDateFormats = new[]
        {
            "yyyy-MM-dd",
            "M-d-yy",
            "M/d/yy",
            "MM/dd/yyyy",
            "yyyy/MM/dd"
        };

        /// <summary>Attempts to parse a date string in supported formats (yyyy-MM-dd, M-d-yy, etc).</summary>
        /// <returns>The parsed DateTime if successful; null if the string is null, empty, or unparseable.</returns>
        public static DateTime? TryParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            if (DateTime.TryParseExact(dateString, SupportedDateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>Parses a date string with a fallback value if parsing fails.</summary>
        /// <param name="dateString">The string to parse (supports yyyy-MM-dd, M-d-yy, etc).</param>
        /// <param name="fallback">The value to return if parsing fails.</param>
        /// <returns>The parsed DateTime or the fallback value.</returns>
        public static DateTime ParseDateOrDefault(string? dateString, DateTime fallback)
        {
            return TryParseDate(dateString) ?? fallback;
        }

        /// <summary>Formats a DateTime as a standard date string (yyyy-MM-dd).</summary>
        public static string FormatDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }
    }
}
