namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Which half of the world a camera is in, for season mapping.</summary>
    public enum Hemisphere
    {
        Northern,
        Southern
    }

    /// <summary>Result of comparing a device's earliest and latest clips.</summary>
    /// <param name="Drifting">True when the median has moved by more than <see cref="DriftDetector.DriftFraction"/>.</param>
    /// <param name="ChangeFraction">Relative change in median bitrate, earliest to latest window.</param>
    public sealed record DriftReport(bool Drifting, double ChangeFraction);

    /// <summary>
    /// Detects a device whose bitrate median moves steadily across its history. Drift is reported for review and is not
    /// folded into the baseline, so a slow change cannot quietly become the new normal.
    /// </summary>
    public static class DriftDetector
    {
        /// <summary>Clips in each window compared (earliest and latest).</summary>
        public const int WindowClips = 20;

        /// <summary>A relative median change above this is reported as drift.</summary>
        public const double DriftFraction = 0.15;

        /// <summary>Compares the median of the earliest window with the latest. Input is in recording order, oldest first.</summary>
        public static DriftReport Detect(IReadOnlyList<long> bitratesOldestFirst)
        {
            if (bitratesOldestFirst.Count < WindowClips * 2)
            {
                return new DriftReport(Drifting: false, ChangeFraction: 0);
            }

            double earliest = Median(bitratesOldestFirst.Take(WindowClips));
            double latest = Median(bitratesOldestFirst.Skip(bitratesOldestFirst.Count - WindowClips));
            double change = earliest == 0 ? 0 : Math.Abs(latest - earliest) / earliest;
            return new DriftReport(Drifting: change > DriftFraction, ChangeFraction: change);
        }

        private static double Median(IEnumerable<long> values)
        {
            List<long> sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }
    }
}
