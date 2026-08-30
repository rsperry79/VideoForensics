namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Phase 1 Summary: Timeline & Patterns - fast decision point before detail queries.
    /// ComplianceScore (inherited) is intentionally left null: a coverage % averaged across
    /// devices would let one healthy camera mask another's gaps. See <see cref="DeviceSummaries"/>
    /// for each device's own numbers; Status reflects the worst device (any camera Critical ->
    /// overall Critical), which is a categorical triage signal, not an average.
    /// </summary>
    public class TimelineSummary : QuerySummary
    {
        public List<DeviceTimelineSummary> DeviceSummaries { get; set; } = new();
        public List<string> SuspiciousDevices { get; set; } = new();
        public List<HourlyActivityCount> PeakHours { get; set; } = new();
    }

    /// <summary>One device's own timeline summary numbers - never blended with any other device's.</summary>
    public class DeviceTimelineSummary
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public int GapCount { get; set; }
        public int LargestGapMinutes { get; set; }
        public decimal CoveragePercentage { get; set; }
        public string Status { get; set; } = string.Empty; // "Healthy", "Anomalies", "Critical"
    }

    /// <summary>Phase 2 Summary: Evidence Integrity - quick compliance check.</summary>
    public class IntegritySummary : QuerySummary
    {
        public int TamperingIndicators { get; set; }
        public int MissingDownloads { get; set; }
        public int FailedRecordings { get; set; }
        public decimal IntegrityScore { get; set; } // 0-100%
        public List<string> CompromisedDevices { get; set; } = new();
    }

    /// <summary>
    /// Phase 3 Summary: Correlation Queries - health & sync status at-a-glance.
    /// ComplianceScore (inherited) is intentionally left null: averaging per-device uptime %
    /// would let a healthy camera mask an unhealthy one, which is invalid for evidence. Use
    /// AnalyzeSyncHealthAsync's per-device DeviceStatus list for each camera's own uptime.
    /// </summary>
    public class CorrelationSummary : QuerySummary
    {
        public int DeviceCount { get; set; }
        public int UnhealthyDeviceCount { get; set; }
        public int SyncFailureCount { get; set; }
        public List<string> OfflineDevices { get; set; } = new();
        public List<string> LocationChanges { get; set; } = new();
    }

    /// <summary>Phase 4 Summary: Access & Export Audit - chain of custody status.</summary>
    public class AuditTrailSummary : QuerySummary
    {
        public int AccessCount { get; set; }
        public int UnauthorizedAccessCount { get; set; }
        public int ExportCount { get; set; }
        public DateTime LastAccessUtc { get; set; }
        public DateTime LastExportUtc { get; set; }
        public bool ChainOfCustodyIntact { get; set; }
        public List<string> SuspiciousAccessPatterns { get; set; } = new();
    }
}
