namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>One advanced jamming detection window inside a sync schedule.</summary>
    public sealed record JammingScheduleWindowDto(
        Guid Id,
        int DayOfWeek,
        int StartMinuteOfDay,
        int EndMinuteOfDay,
        int SnapshotRssiIntervalMinutes);

    /// <summary>Validation limits for sync schedule values, shared by client pages and server routes.</summary>
    public static class SyncScheduleLimits
    {
        /// <summary>Minimum time between provider event polls, in minutes.</summary>
        public const int MinimumPollIntervalMinutes = 1;

        /// <summary>Default snapshot interval, in minutes.</summary>
        public const int DefaultSnapshotIntervalMinutes = 2;

        /// <summary>Minimum snapshot interval, in minutes.</summary>
        public const int MinimumSnapshotIntervalMinutes = 1;

        /// <summary>Default event poll interval, in minutes.</summary>
        public const int DefaultEventPollIntervalMinutes = 60;
    }
}
