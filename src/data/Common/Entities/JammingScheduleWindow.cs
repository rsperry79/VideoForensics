namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Represents a time window during which different snapshot/RSSI polling intervals apply for jamming detection.
    /// Used only when SyncSchedule.UseAdvancedJammingSchedule is true.</summary>
    public class JammingScheduleWindow
    {
        /// <summary>Primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>Foreign key to the parent SyncSchedule; windows are meaningless without their schedule.</summary>
        public Guid SyncScheduleId { get; set; }

        /// <summary>Day of week (0=Sunday, 1=Monday, ..., 6=Saturday).
        /// The validation layer (repository Upsert) enforces that this is in range 0..6.</summary>
        public int DayOfWeek { get; set; }

        /// <summary>Start time of this window, as minutes since midnight (0-1439).
        /// Must be a multiple of 15 (15-minute granularity for jamming detection).
        /// Validation is enforced at the application layer (repository Upsert), not the database.</summary>
        public int StartMinuteOfDay { get; set; }

        /// <summary>End time of this window, as minutes since midnight (0-1439).
        /// Must be a multiple of 15 and must be greater than StartMinuteOfDay.
        /// Validation is enforced at the application layer (repository Upsert), not the database.</summary>
        public int EndMinuteOfDay { get; set; }

        /// <summary>Snapshot/RSSI polling interval in minutes during this window.
        /// Overrides the default SnapshotRssiIntervalMinutes while this window is active.
        /// Minimum floor is defined by SyncSchedule.MinimumPollIntervalMinutes (rate-limit safety measure).</summary>
        public int SnapshotRssiIntervalMinutes { get; set; }
    }
}
