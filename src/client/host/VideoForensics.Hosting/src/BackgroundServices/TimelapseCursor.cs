namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Where a device's next time-lapse planning starts: the latest of its latest time-lapse end, its latest event at or before
    /// the horizon, and its earliest capture. Events close gaps, so a gap that wrote no time lapse is still behind the cursor.
    /// </summary>
    public static class TimelapseCursor
    {
        /// <summary>Returns the latest known point, or null when the device has no captures, events or time lapses.</summary>
        public static DateTime? Resolve(DateTime? latestTimelapseEndUtc, DateTime? latestEventUtc, DateTime? earliestCaptureUtc)
        {
            DateTime? latest = null;
            foreach (DateTime? candidate in new[] { latestTimelapseEndUtc, latestEventUtc, earliestCaptureUtc })
            {
                if (candidate is { } value && (latest is null || value > latest))
                {
                    latest = value;
                }
            }

            return latest;
        }
    }
}
