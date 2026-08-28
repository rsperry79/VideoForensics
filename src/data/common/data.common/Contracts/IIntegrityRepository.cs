namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for evidence integrity verification and audit trails.</summary>
    public interface IIntegrityRepository
    {
        /// <summary>Gets download history for a device.</summary>
        Task<IReadOnlyList<DownloadAuditRecord>> GetDownloadHistoryAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets download history for all devices in a location.</summary>
        Task<IReadOnlyList<DownloadAuditRecord>> GetLocationDownloadHistoryAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Identifies events that exist in API but were not downloaded.</summary>
        Task<IReadOnlyList<MissingDownloadRecord>> GetMissingDownloadsAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Verifies download completeness for a location.</summary>
        Task<DownloadCompletenessReport> VerifyDownloadCompletenessAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Verifies event hashes to detect tampering.</summary>
        Task<IReadOnlyList<TamperingIndicator>> VerifyEventHashesAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Gets tampering indicators for a location.</summary>
        Task<IReadOnlyList<TamperingIndicator>> GetTamperingIndicatorsAsync(
            Guid locationId, CancellationToken ct);

        /// <summary>Computes overall integrity score for a location (0-100%).</summary>
        Task<int> ComputeEventIntegrityScoreAsync(Guid locationId, CancellationToken ct);

        /// <summary>Detects missing events using baseline patterns (abnormal gaps).</summary>
        Task<IReadOnlyList<AnomalousGap>> DetectMissingEventsByPatternAsync(
            Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Identifies recording failures (device offline, battery, connectivity).</summary>
        Task<IReadOnlyList<RecordingFailure>> IdentifyRecordingFailuresAsync(
            Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>Flags suspicious gaps during critical periods.</summary>
        Task<IReadOnlyList<SuspiciousGap>> FlagSuspiciousGapsAsync(Guid locationId, CancellationToken ct);
    }

    /// <summary>Download audit record for chain of custody.</summary>
    public class DownloadAuditRecord
    {
        public Guid EventId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public DateTime? DownloadedAtUtc { get; set; }
        public int DelayMinutes { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string DownloadStatus { get; set; } = "Downloaded"; // "Downloaded", "Missing"
    }

    /// <summary>Record of event not downloaded.</summary>
    public class MissingDownloadRecord
    {
        public Guid EventId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public DateTime DiscoveredAtUtc { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Reason { get; set; } = "Unknown"; // "DeviceOffline", "DownloadFailed", "NotRequested"
    }

    /// <summary>Download completeness report.</summary>
    public class DownloadCompletenessReport
    {
        public Guid LocationId { get; set; }
        public DateTime AnalysisFromUtc { get; set; }
        public DateTime AnalysisToUtc { get; set; }
        public int TotalEvents { get; set; }
        public int DownloadedEvents { get; set; }
        public int MissingEvents { get; set; }
        public decimal CompletenessPercentage { get; set; }
        public List<MissingDownloadRecord> MissingRecords { get; set; } = new();
        public string Status { get; set; } = "Complete"; // "Complete", "Incomplete", "Critical"
    }

    /// <summary>Indicator of potential tampering.</summary>
    public class TamperingIndicator
    {
        public Guid EventId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public string IndicatorType { get; set; } = string.Empty; // "HashMismatch", "TimestampAnomaly", "SourceMismatch"
        public string Description { get; set; } = string.Empty;
        public int TamperingScore { get; set; } // 1-100
    }

    /// <summary>Anomalous gap detected via pattern analysis.</summary>
    public class AnomalousGap
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int DurationMinutes { get; set; }
        public decimal BaselineEventRate { get; set; } // events/hour
        public decimal ExpectedEvents { get; set; }
        public int ActualEvents { get; set; }
        public string Anomaly { get; set; } = "Missing"; // "Missing", "Excess"
    }

    /// <summary>Identified recording failure cause.</summary>
    public class RecordingFailure
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime FailureAtUtc { get; set; }
        public DateTime? RecoveryAtUtc { get; set; }
        public int DurationMinutes { get; set; }
        public string FailureType { get; set; } = string.Empty; // "DeviceOffline", "LowBattery", "NoConnectivity", "Unknown"
        public string Evidence { get; set; } = string.Empty;
    }

    /// <summary>Gap occurring during suspicious time (critical moments).</summary>
    public class SuspiciousGap
    {
        public Guid LocationId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime GapStartUtc { get; set; }
        public DateTime GapEndUtc { get; set; }
        public int DurationMinutes { get; set; }
        public string Suspicion { get; set; } = string.Empty; // "NightGap", "WeekendGap", "LongGap", "MultiDeviceGap"
        public int SuspicionScore { get; set; } // 1-100
    }
}
