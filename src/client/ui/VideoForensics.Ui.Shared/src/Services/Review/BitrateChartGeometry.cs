using VideoForensics.Core.Api.Contracts;

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

        /// <summary>
        /// A second counts as a dip when its bitrate is below this share of the clip's median bitrate. Half the median keeps
        /// ordinary variation out of the marks while still catching a real collapse.
        /// </summary>
        public const double DipFraction = 0.5;

        /// <summary>The playback position, in seconds, that a click on a second seeks to: the middle of that second.</summary>
        /// <param name="second">The clicked second of the clip.</param>
        public static double SeekSeconds(int second)
        {
            return second + 0.5;
        }

        /// <summary>
        /// The first second of each run of consecutive dip seconds, oldest first. Each run gets one mark, so a clip with many
        /// separate dips shows a mark for each.
        /// </summary>
        /// <param name="points">The clip's per-second bitrate.</param>
        public static IReadOnlyList<int> DipRunStarts(IReadOnlyList<BitrateSecondDto> points)
        {
            if (points.Count == 0)
            {
                return [];
            }

            var sorted = points.Select(p => p.Kbps).OrderBy(kbps => kbps).ToList();
            int middle = sorted.Count / 2;
            double median = sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
            double threshold = median * DipFraction;

            var starts = new List<int>();
            bool inDip = false;
            foreach (BitrateSecondDto point in points)
            {
                bool dip = point.Kbps < threshold;
                if (dip && !inDip)
                {
                    starts.Add(point.Second);
                }

                inDip = dip;
            }

            return starts;
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
