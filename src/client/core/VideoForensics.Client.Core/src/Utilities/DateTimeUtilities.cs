using NodaTime;
using NodaTime.Text;

namespace VideoForensics.Client.Core.Utilities
{
    /// <summary>Helper methods for parsing and formatting date/time strings using NodaTime for robust, culture-aware parsing.</summary>
    public static class DateTimeUtilities
    {
        /// <summary>
        /// Supported date format patterns for client-side UI input.
        /// Uses NodaTime for robust, culturally-aware parsing of multiple formats.
        /// Note: API response parsing (Ring, Wyze) is NOT handled here; those use provider-specific logic.
        /// </summary>
        private static readonly LocalDatePattern[] SupportedPatterns = new[]
        {
            LocalDatePattern.CreateWithInvariantCulture("yyyy-MM-dd"),
            LocalDatePattern.CreateWithInvariantCulture("M-d-yy"),
            LocalDatePattern.CreateWithInvariantCulture("M/d/yy"),
            LocalDatePattern.CreateWithInvariantCulture("MM/dd/yyyy"),
            LocalDatePattern.CreateWithInvariantCulture("yyyy/MM/dd"),
        };

        /// <summary>Attempts to parse a date string in supported formats (yyyy-MM-dd, M-d-yy, etc).</summary>
        /// <remarks>
        /// Uses NodaTime for more robust and culturally-aware parsing compared to DateTime.TryParseExact.
        /// Only for client-side UI date input; API response parsing uses provider-specific logic.
        /// </remarks>
        /// <returns>The parsed DateTime if successful; null if the string is null, empty, or unparseable.</returns>
        public static DateTime? TryParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            foreach (var pattern in SupportedPatterns)
            {
                var parseResult = pattern.Parse(dateString);
                if (parseResult.Success)
                {
                    var localDate = parseResult.Value;
                    return new DateTime(localDate.Year, localDate.Month, localDate.Day);
                }
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
