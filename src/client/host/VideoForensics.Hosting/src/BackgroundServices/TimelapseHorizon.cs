namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// How far a time lapse may be planned. Events and snapshots can arrive late, so a segment is never planned past the last
    /// clean event pull minus the lag, or past the last snapshot sync. With either unknown, nothing is planned.
    /// </summary>
    public static class TimelapseHorizon
    {
        /// <param name="lastCleanPullUtc">Completion of the last event pull that had no errors, or null if none yet.</param>
        /// <param name="lastSnapshotSyncUtc">Completion of the last snapshot sync, or null if none yet.</param>
        /// <param name="lag">Safety lag subtracted from the pull time, to cover late-arriving events.</param>
        public static DateTime? Compute(DateTime? lastCleanPullUtc, DateTime? lastSnapshotSyncUtc, TimeSpan lag)
        {
            if (lag < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(lag), "The lag cannot be negative.");
            }

            if (lastCleanPullUtc is null || lastSnapshotSyncUtc is null)
            {
                return null;
            }

            DateTime pullHorizon = lastCleanPullUtc.Value - lag;
            return pullHorizon <= lastSnapshotSyncUtc.Value ? pullHorizon : lastSnapshotSyncUtc.Value;
        }
    }
}
