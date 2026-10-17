namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One row per account sync attempt (manual or initial), recording when it ran, whether it succeeded, and the
    /// failure message if it did not. Lets the account details page list past syncs.
    /// </summary>
    public class ProviderSyncRun
    {
        /// <summary>Primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>The provider account this sync ran for.</summary>
        public Guid ProviderAccountId { get; set; }

        /// <summary>UTC time the sync started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC time the sync finished (success or failure); null only if the row was written before completion.</summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>True only when credentials restored and the event pull finished without recording an account error.</summary>
        public bool Success { get; set; }

        /// <summary>Failure message for an unsuccessful run; null when Success is true. Truncated to 1024 characters at write time.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// "Full" when the config gate ran this sync, "Events" when only event feeds ran, "Unknown" for runs that failed
        /// before any pull and for rows written before this column existed.
        /// </summary>
        public string SyncType { get; set; } = "Unknown";

        /// <summary>Locations the pull discovered; null for rows written before counts were recorded or runs that never pulled.</summary>
        public int? LocationsSynced { get; set; }

        /// <summary>Devices the pull discovered; null when counts were not recorded.</summary>
        public int? DevicesSynced { get; set; }

        /// <summary>Event-feed items written; null when counts were not recorded.</summary>
        public int? EventsSynced { get; set; }

        /// <summary>Events whose media was not pulled (no recording, failed, or still throttled); null when not recorded.</summary>
        public int? EventsNotPulled { get; set; }

        /// <summary>The part of EventsSynced that was already on disk from an earlier run and not fetched again; null when not recorded.</summary>
        public int? EventsAlreadyPresent { get; set; }

        /// <summary>Config areas written (profile, ringtones, light groups, mode, mode settings, mode sharing); null when not recorded.</summary>
        public int? ConfigAreasSynced { get; set; }

        /// <summary>Feed-level failures during the sync; null when not recorded.</summary>
        public int? FeedsFailed { get; set; }
    }
}
