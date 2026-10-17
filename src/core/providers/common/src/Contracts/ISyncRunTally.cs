namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Counts what one account sync wrote, so the runner can record it on the sync run row. Scoped per DI scope: the
    /// runner resets it before a pull and reads it after. Providers and the event pull report into it; no sync logic
    /// depends on it, so a missing registration only means zero counts.
    /// </summary>
    public interface ISyncRunTally
    {
        /// <summary>Clears every count and flag. Call before each pull so a previous run's counts never leak in.</summary>
        void Reset();

        /// <summary>Records that the provider's config (settings) gate was due and the config fetches ran this run.</summary>
        void MarkConfigRan();

        /// <summary>Records one config area (profile, ringtones, light groups, mode, mode settings or mode sharing) that was written.</summary>
        void AddConfigArea();

        /// <summary>Records events whose media was downloaded in this run.</summary>
        void AddEventsPulled(int count);

        /// <summary>Records events whose media was already on disk from an earlier run (counted as pulled, reported separately).</summary>
        void AddEventsAlreadyPresent(int count);

        /// <summary>Records events that matched the sync but whose media was not pulled (no recording, failed, or not attempted).</summary>
        void AddEventsNotPulled(int count);

        /// <summary>Records one feed-level failure. Feeds that are reported as not available (not found) are not failures.</summary>
        void AddFeedFailure();

        /// <summary>Records the locations and devices the pull worked from.</summary>
        void SetInventory(int locationCount, int deviceCount);

        /// <summary>Returns the counts recorded since the last <see cref="Reset"/>.</summary>
        SyncRunTotals Snapshot();
    }

    /// <summary>Immutable snapshot of one sync's counts, as stored on the sync run row.</summary>
    /// <param name="ConfigRan">True when the config gate was due and config fetches ran.</param>
    /// <param name="LocationsSynced">Locations the pull discovered.</param>
    /// <param name="DevicesSynced">Devices the pull discovered.</param>
    /// <param name="ConfigAreasSynced">Config areas written.</param>
    /// <param name="EventsSynced">Events counted as pulled: downloaded in this run plus already present on disk.</param>
    /// <param name="FeedsFailed">Feed-level failures.</param>
    /// <param name="EventsNotPulled">Matched events whose media was not pulled.</param>
    /// <param name="EventsAlreadyPresent">The part of EventsSynced that was already on disk and not fetched again.</param>
    public sealed record SyncRunTotals(
        bool ConfigRan,
        int LocationsSynced,
        int DevicesSynced,
        int ConfigAreasSynced,
        int EventsSynced,
        int FeedsFailed,
        int EventsNotPulled = 0,
        int EventsAlreadyPresent = 0);
}
