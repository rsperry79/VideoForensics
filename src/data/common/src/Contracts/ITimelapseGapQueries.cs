namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Queries the event-gap time-lapse planner needs. Local database only.</summary>
    public interface ITimelapseGapQueries
    {
        /// <summary>
        /// The occurrence times of a device's events in [fromUtc, toUtc], ascending. The device's own time-lapse events
        /// (EventType "timelapse") are excluded, so the planner never plans around its own output.
        /// </summary>
        Task<IReadOnlyList<DateTime>> ListEventTimesForGapsAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>
        /// The end of the device's latest time lapse: its start plus its duration. Null when the device has none. This is the
        /// per-device cursor, so it needs no separate table.
        /// </summary>
        Task<DateTime?> GetLatestTimelapseEndUtcAsync(Guid deviceId, CancellationToken ct);
    }
}
