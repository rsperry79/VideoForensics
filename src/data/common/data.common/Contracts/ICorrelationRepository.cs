namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for correlating events with device health and status.</summary>
    public interface ICorrelationRepository
    {
        /// <summary>Gets events with correlated health data.</summary>
        Task<IReadOnlyList<EventWithHealthCorrelation>> GetEventHealthCorrelationAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Identifies gaps caused by device health issues.</summary>
        Task<IReadOnlyList<HealthRelatedGap>> IdentifyHealthRelatedGapsAsync(Guid locationId, CancellationToken ct);

        /// <summary>Analyzes device reliability (uptime vs event capture).</summary>
        Task<DeviceReliabilityAnalysis> AnalyzeDeviceReliabilityAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets location change history and impact.</summary>
        Task<IReadOnlyList<LocationChangeImpact>> GetLocationChangeHistoryAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Correlates location changes with event gaps.</summary>
        Task<IReadOnlyList<LocationChangeWithGap>> CorrelateLocationChangeWithGapsAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets sync status correlation with missing events.</summary>
        Task<IReadOnlyList<SyncGapCorrelation>> CorrelateEventMissingWithSyncGapsAsync(Guid locationId, CancellationToken ct);

        /// <summary>Overall sync health analysis for a location.</summary>
        Task<SyncHealthReport> AnalyzeSyncHealthAsync(Guid locationId, CancellationToken ct);
    }

    /// <summary>Event with correlated device health data.</summary>
    public class EventWithHealthCorrelation
    {
        public Guid EventId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string EventType { get; set; } = string.Empty;
        public decimal? BatteryPercentage { get; set; }
        public int? WifiSignalRssi { get; set; }
        public bool? IsOnline { get; set; }
        public string HealthStatus { get; set; } = "Good"; // "Good", "Degraded", "Critical"
    }

    /// <summary>Gap caused by device health issue.</summary>
    public class HealthRelatedGap
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime GapStartUtc { get; set; }
        public int DurationMinutes { get; set; }
        public string HealthIssue { get; set; } = string.Empty; // "LowBattery", "OfflineStatus", "PoorWiFi"
        public decimal? MinBattery { get; set; }
        public int? MinRssi { get; set; }
    }

    /// <summary>Device reliability metrics.</summary>
    public class DeviceReliabilityAnalysis
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public decimal UptimePercentage { get; set; }
        public decimal EventCaptureRate { get; set; }
        public int TotalGaps { get; set; }
        public int HealthRelatedGaps { get; set; }
        public string ReliabilityRating { get; set; } = "Unknown"; // "Excellent", "Good", "Fair", "Poor"
    }

    /// <summary>Location change and its impact.</summary>
    public class LocationChangeImpact
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime ChangedAtUtc { get; set; }
        public string OldLocation { get; set; } = string.Empty;
        public string NewLocation { get; set; } = string.Empty;
        public DateTime? FirstGapAfterMove { get; set; }
        public string Impact { get; set; } = "Unknown"; // "NoImpact", "MinorImpact", "SignificantImpact"
    }

    /// <summary>Location change correlated with event gaps.</summary>
    public class LocationChangeWithGap
    {
        public Guid DeviceId { get; set; }
        public DateTime LocationChangeUtc { get; set; }
        public DateTime GapStartUtc { get; set; }
        public int DaysAfterMove { get; set; }
        public int GapDurationMinutes { get; set; }
        public bool LikelyCorrelated { get; set; }
    }

    /// <summary>Sync gap correlated with missing events.</summary>
    public class SyncGapCorrelation
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime SyncGapStartUtc { get; set; }
        public int SyncGapMinutes { get; set; }
        public int MissingEventsDuring { get; set; }
        public bool CauseLikely { get; set; }
    }

    /// <summary>Overall sync health for a location.</summary>
    public class SyncHealthReport
    {
        public Guid LocationId { get; set; }
        public int DeviceCount { get; set; }
        public decimal AverageUptime { get; set; }
        public int TotalSyncGaps { get; set; }
        public DateTime LastSuccessfulSyncUtc { get; set; }
        public string HealthStatus { get; set; } = "Unknown"; // "Healthy", "Degraded", "Critical"
        public List<(Guid DeviceId, string DeviceName, decimal Uptime)> DeviceStatus { get; set; } = new();
    }
}
