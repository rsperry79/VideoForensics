namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Tracks the sync schedule for a provider account, including separate intervals for events and snapshots/RSSI data.</summary>
    public class SyncSchedule
    {
        /// <summary>Primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>Foreign key to the provider account; globally unique per account.</summary>
        public Guid ProviderAccountId { get; set; }

        /// <summary>Interval in minutes for pulling events (e.g., rule violations, motion, person detection).
        /// This interval is independent of snapshot/RSSI polling.
        /// Minimum floor is defined by MinimumSnapshotIntervalSeconds (rate-limit safety measure).</summary>
        public int EventPollIntervalMinutes { get; set; }

        /// <summary>Interval in seconds for pulling snapshots and RSSI data.
        /// This is the DEFAULT interval used when UseAdvancedJammingSchedule is false or no active jamming window applies.
        /// Minimum floor is defined by MinimumSnapshotIntervalSeconds (rate-limit safety measure).</summary>
        public int SnapshotRssiIntervalSeconds { get; set; }

        /// <summary>Spacing in seconds between snapshot requests for this account. Null means not configured: the effective
        /// spacing is then the media pacer's current MinInterval. Raised by one second each time Ring starts a new rate-limit
        /// incident for the account (see RingMediaDownloadService), so the stored spacing settles at a rate Ring accepts.</summary>
        public int? SnapshotIntervalSeconds { get; set; }

        /// <summary>Indicates whether syncing is enabled for this account.</summary>
        public bool IsEnabled { get; set; }

        /// <summary>Indicates whether the advanced jamming detection schedule (JammingScheduleWindows) is active.
        /// When false, only SnapshotRssiIntervalSeconds is used.
        /// When true, JammingScheduleWindows override SnapshotRssiIntervalSeconds during their active windows.</summary>
        public bool UseAdvancedJammingSchedule { get; set; }

        /// <summary>The UTC timestamp when the next event poll is scheduled to run; null if no future event poll is scheduled.</summary>
        public DateTime? EventNextRunUtc { get; set; }

        /// <summary>The UTC timestamp of the most recent event poll run; null if never run.</summary>
        public DateTime? EventLastRunUtc { get; set; }

        /// <summary>The UTC timestamp when the next snapshot/RSSI poll is scheduled to run; null if no future poll is scheduled.</summary>
        public DateTime? SnapshotNextRunUtc { get; set; }

        /// <summary>The UTC timestamp of the most recent snapshot/RSSI poll run; null if never run.</summary>
        public DateTime? SnapshotLastRunUtc { get; set; }

        /// <summary>Navigation property: jamming detection schedule windows (empty if UseAdvancedJammingSchedule is false).</summary>
        public ICollection<JammingScheduleWindow> JammingWindows { get; set; } = [];

        /// <summary>Minimum poll interval in minutes (rate-limit safety floor, not a provider-specific quota).
        /// This constant ensures we never hit provider API rate limiters with excessively tight polling.
        /// Tune this value in one place if future provider quotas require adjustment.</summary>
        public const int MinimumPollIntervalMinutes = 1;

        /// <summary>Default snapshot/RSSI interval for a newly linked account: one minute, stored in seconds.</summary>
        public const int DefaultSnapshotIntervalSeconds = 60;

        /// <summary>Snapshot/RSSI floor in seconds. One second is the smallest interval an operator can set.</summary>
        public const int MinimumSnapshotIntervalSeconds = 1;

/// <summary>Default event poll interval for a newly linked account, in minutes.</summary>
        public const int DefaultEventPollIntervalMinutes = 60;

        /// <summary>
        /// The schedule every newly linked account gets: enabled, default interval, and due on the next scheduler tick
        /// (EventNextRunUtc left null).
        /// </summary>
        public static SyncSchedule CreateDefault(Guid providerAccountId)
        {
            return new SyncSchedule
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = providerAccountId,
                EventPollIntervalMinutes = DefaultEventPollIntervalMinutes,
                SnapshotRssiIntervalSeconds = DefaultSnapshotIntervalSeconds,
                IsEnabled = true,
                UseAdvancedJammingSchedule = false
            };
        }
    }
}
