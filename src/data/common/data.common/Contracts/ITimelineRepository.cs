using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for timeline analysis and forensic gap detection.</summary>
    public interface ITimelineRepository
    {
        /// <summary>Detects recording gaps for a device (periods with no events).</summary>
        /// <param name="deviceId">Device to analyze</param>
        /// <param name="fromUtc">Start of analysis period</param>
        /// <param name="toUtc">End of analysis period</param>
        /// <param name="minGapMinutes">Minimum gap duration to report</param>
        Task<IReadOnlyList<TimelineGap>> GetRecordingGapsAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, CancellationToken ct);

        /// <summary>Detects all gaps for a location (all devices).</summary>
        Task<IReadOnlyList<TimelineGap>> GetLocationRecordingGapsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, int minGapMinutes, CancellationToken ct);

        /// <summary>Gets event count by hour for timeline heatmap.</summary>
        Task<Dictionary<int, int>> GetEventCountByHourAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets event count by day for activity pattern analysis.</summary>
        Task<Dictionary<string, int>> GetEventCountByDayAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets peak activity periods (hours with most events).</summary>
        Task<IReadOnlyList<(int Hour, int Count)>> GetPeakActivityPeriodsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Verifies timeline integrity and returns forensic report.</summary>
        Task<TimelineIntegrityReport> VerifyTimelineIntegrityAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Finds events from multiple devices occurring within a time window (coordinated activity).</summary>
        Task<IReadOnlyList<CoordinatedEventCluster>> GetCoordinatedEventsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, int timeWindowSeconds, CancellationToken ct);

        /// <summary>Flags suspicious coordinated activity across devices.</summary>
        Task<IReadOnlyList<SuspiciousActivityFlag>> FindSuspiciousCoordinatedActivityAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    }

    /// <summary>Represents a gap in event recording.</summary>
    public class TimelineGap
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int DurationMinutes { get; set; }
        public int EventsBeforeGap { get; set; }
        public int EventsAfterGap { get; set; }
    }

    /// <summary>Forensic timeline integrity report.</summary>
    public class TimelineIntegrityReport
    {
        public Guid LocationId { get; set; }
        public DateTime AnalysisFromUtc { get; set; }
        public DateTime AnalysisToUtc { get; set; }
        public int TotalEvents { get; set; }
        public int TotalGaps { get; set; }
        public int LargestGapMinutes { get; set; }
        public decimal CoveragePercentage { get; set; }
        public IReadOnlyList<TimelineGap> SignificantGaps { get; set; } = new List<TimelineGap>();
        public Dictionary<string, int> EventTypeDistribution { get; set; } = new();
        public string IntegrityStatus { get; set; } = "Unknown"; // "Intact", "Gaps", "Critical"
    }

    /// <summary>Group of events from multiple devices within a time window.</summary>
    public class CoordinatedEventCluster
    {
        public DateTime ClusterTimeUtc { get; set; }
        public int DeviceCount { get; set; }
        public int TotalEventCount { get; set; }
        public List<(Guid DeviceId, string DeviceName, string EventType, DateTime OccurredAtUtc)> Events { get; set; } = new();
    }

    /// <summary>Suspicious pattern detected (potential tampering or coordinated action).</summary>
    public class SuspiciousActivityFlag
    {
        public Guid LocationId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string ActivityType { get; set; } = string.Empty; // "SimultaneousMotion", "CameraDisabledDuringMotion", "MultipleDeviceGap"
        public string Description { get; set; } = string.Empty;
        public int SuspicionScore { get; set; } // 1-100
        public List<(Guid DeviceId, string DeviceName)> InvolvedDevices { get; set; } = new();
    }
}
