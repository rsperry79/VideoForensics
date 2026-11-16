namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>One advanced jamming detection window inside a sync schedule.</summary>
    public sealed record JammingScheduleWindowDto(
        Guid Id,
        int DayOfWeek,
        int StartMinuteOfDay,
        int EndMinuteOfDay,
        int SnapshotRssiIntervalSeconds);

    /// <summary>Validation limits for sync schedule values, shared by client pages and server routes.</summary>
    public static class SyncScheduleLimits
    {
        /// <summary>Minimum time between provider event polls, in minutes.</summary>
        public const int MinimumPollIntervalMinutes = 1;

        /// <summary>Default snapshot interval, in seconds.</summary>
        public const int DefaultSnapshotIntervalSeconds = 60;

        /// <summary>Minimum snapshot interval, in seconds.</summary>
        public const int MinimumSnapshotIntervalSeconds = 1;

        /// <summary>Default event poll interval, in minutes.</summary>
        public const int DefaultEventPollIntervalMinutes = 60;
    }
}
