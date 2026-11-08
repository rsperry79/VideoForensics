namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// A camera's all-time bit rate baseline: how many stream-reported clip bitrates it has, and their mean and sample std dev.
    /// Built from every reading the camera has, with no date window, so it is the camera-wide figure and not the per-season one.
    /// </summary>
    /// <param name="Count">How many readings the baseline covers. Zero when the camera has none.</param>
    /// <param name="MeanBps">Mean video bitrate in bits per second. Zero when there are no readings.</param>
    /// <param name="StdDevBps">Sample std dev (divides by n - 1) in bits per second. Null with fewer than two readings.</param>
    public sealed record AllTimeBitrateBaseline(int Count, double MeanBps, double? StdDevBps)
    {
        /// <summary>
        /// Builds the baseline from the three aggregates a query returns, so every caller gets the same arithmetic. The sample
        /// variance is (sum of squares - n * mean^2) / (n - 1), clamped at zero so rounding never yields a NaN std dev.
        /// </summary>
        /// <param name="count">Number of readings.</param>
        /// <param name="sum">Sum of the readings in bits per second.</param>
        /// <param name="sumOfSquares">Sum of the squared readings in bits per second, as double so it cannot overflow.</param>
        public static AllTimeBitrateBaseline FromTotals(int count, double sum, double sumOfSquares)
        {
            if (count <= 0)
            {
                return new AllTimeBitrateBaseline(0, 0, null);
            }

            double mean = sum / count;
            double? stdDev = count < 2
                ? null
                : Math.Sqrt(Math.Max(0, (sumOfSquares - count * mean * mean) / (count - 1)));
            return new AllTimeBitrateBaseline(count, mean, stdDev);
        }
    }
}
