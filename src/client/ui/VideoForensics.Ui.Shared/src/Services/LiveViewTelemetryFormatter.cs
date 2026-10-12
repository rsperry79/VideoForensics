using System.Globalization;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Formats the latest live-view telemetry values for display. Numbers are always invariant-culture text, so a
    /// comma-decimal locale cannot change them. Units and labels are localized by the page, not here.
    /// </summary>
    public static class LiveViewTelemetryFormatter
    {
        /// <summary>
        /// Returned for a value the server did not report. The page translates this key through its localizer;
        /// a formatted number can never equal it.
        /// </summary>
        public const string NotAvailableKey = "NotAvailable";

        /// <summary>Formats an interference score to two decimals, or <see cref="NotAvailableKey"/> when null.</summary>
        /// <param name="score">The score, or null if not scored.</param>
        public static string FormatScore(double? score) =>
            score is null ? NotAvailableKey : score.Value.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>
        /// Converts an RTCP fraction-lost byte (0-255) to a percentage with one decimal, or <see cref="NotAvailableKey"/> when null.
        /// </summary>
        /// <param name="fractionLost">The RTCP fraction-lost value, or null if not reported.</param>
        public static string FormatLossPercent(byte? fractionLost) =>
            fractionLost is null
                ? NotAvailableKey
                : (fractionLost.Value / 255.0 * 100.0).ToString("F1", CultureInfo.InvariantCulture);

        /// <summary>Converts bits per second to whole kilobits per second, or <see cref="NotAvailableKey"/> when null.</summary>
        /// <param name="bitsPerSecond">The bitrate in bits per second, or null if not measured.</param>
        public static string FormatKilobitsPerSecond(long? bitsPerSecond) =>
            bitsPerSecond is null
                ? NotAvailableKey
                : (bitsPerSecond.Value / 1000.0).ToString("F0", CultureInfo.InvariantCulture);

        /// <summary>Formats an integer count (packets lost, jitter ticks), or <see cref="NotAvailableKey"/> when null.</summary>
        /// <param name="value">The count, or null if not reported.</param>
        public static string FormatCount(long? value) =>
            value is null ? NotAvailableKey : value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
