namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Phase 1 Summary: Timeline & Patterns - fast decision point before detail queries.</summary>
    public class TimelineSummary : QuerySummary
    {
        public int GapCount { get; set; }
        public int LargestGapMinutes { get; set; }
        public decimal CoveragePercentage { get; set; }
        public List<string> SuspiciousDevices { get; set; } = new();
        public List<(int Hour, int Count)> PeakHours { get; set; } = new();
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

    /// <summary>Phase 3 Summary: Correlation Queries - health & sync status at-a-glance.</summary>
    public class CorrelationSummary : QuerySummary
    {
        public int DeviceCount { get; set; }
        public int UnhealthyDeviceCount { get; set; }
        public int SyncFailureCount { get; set; }
        public decimal AverageDeviceUptime { get; set; } // 0-100%
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
