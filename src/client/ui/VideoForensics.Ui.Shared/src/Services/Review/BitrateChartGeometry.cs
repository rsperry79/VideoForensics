namespace VideoForensics.Client.Ui.Shared.Services.Review
{
    /// <summary>
    /// Maps bitrate values and playback time to fractions of the chart's width or height. Results are clamped to 0..1 so a
    /// bar or playhead never leaves the chart, even when the playback position is briefly past the series.
    /// </summary>
    public static class BitrateChartGeometry
    {
        /// <summary>A bar's height as a fraction of the tallest bar. Zero when there is no tallest bar.</summary>
        /// <param name="kbps">The bar's bitrate in kilobits per second.</param>
        /// <param name="maxKbps">The largest bitrate on the chart.</param>
        public static double BarFraction(long kbps, long maxKbps)
        {
            return maxKbps <= 0 ? 0 : Math.Clamp(kbps / (double)maxKbps, 0, 1);
        }

        /// <summary>How far through the series the playhead is. Zero when the series has no seconds.</summary>
        /// <param name="seconds">Playback position in seconds from the start of the clip.</param>
        /// <param name="secondCount">Number of seconds in the series.</param>
        public static double PlayheadFraction(double seconds, int secondCount)
        {
            return secondCount <= 0 ? 0 : Math.Clamp(seconds / secondCount, 0, 1);
        }
    }
}
