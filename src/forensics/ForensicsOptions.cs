using System;

namespace Ring.Api.Forensics
{
    /// <summary>
    /// Configuration options for forensic analysis operations.
    /// These settings control behavior at the client application level.
    /// </summary>
    public class ForensicsOptions
    {
        /// <summary>
        /// Enable integrity validation during extraction.
        /// </summary>
        public bool ValidateIntegrity { get; set; } = true;

        /// <summary>
        /// Include chain of custody tracking in analysis.
        /// </summary>
        public bool IncludeChainOfCustody { get; set; } = true;

        /// <summary>
        /// Maximum events to process in a single batch.
        /// </summary>
        public int MaxEventsPerBatch { get; set; } = 1000;

        /// <summary>
        /// Time range filter for event analysis.
        /// </summary>
        public TimeSpan? TimeRangeFilter { get; set; }

        /// <summary>
        /// Standard deviations from mean to flag as anomalous.
        /// </summary>
        public double RssiAnomalyThreshold { get; set; } = 2.0;

        /// <summary>
        /// Minimum duration (milliseconds) to consider as jamming incident.
        /// </summary>
        public int MinJammingDurationMs { get; set; } = 10000;

        /// <summary>
        /// Percentage of events that must be anomalous to flag as jamming.
        /// </summary>
        public double JammingAnomalyPercentageThreshold { get; set; } = 0.5;

        // Report Generation Configuration (Client Level)

        /// <summary>
        /// Enable generation of forensic analysis reports.
        /// Default: true (all reports enabled by default).
        /// </summary>
        public bool EnableForensicAnalysisReports { get; set; } = true;

        /// <summary>
        /// Enable generation of signal anomaly reports for tamper/jamming detection.
        /// Default: true (critical for DV victim protection).
        /// </summary>
        public bool EnableSignalAnomalyReports { get; set; } = true;

        /// <summary>
        /// Enable generation of chain of custody reports.
        /// Default: true (important for legal proceedings).
        /// </summary>
        public bool EnableChainOfCustodyReports { get; set; } = true;

        /// <summary>
        /// Enable generation of evidence validation reports.
        /// Default: true (ensures compliance).
        /// </summary>
        public bool EnableEvidenceValidationReports { get; set; } = true;

        /// <summary>
        /// Default format for storing reports (JSON, CSV, etc).
        /// Client application uses this as default when persisting reports.
        /// Default: "json"
        /// </summary>
        public string DefaultReportFormat { get; set; } = "json";

        /// <summary>
        /// Directory path where client should persist generated reports.
        /// If null, client decides the storage location.
        /// </summary>
        public string? ReportStoragePath { get; set; }

        /// <summary>
        /// Whether to compress reports (gzip) when storing.
        /// Client respects this preference.
        /// </summary>
        public bool CompressReports { get; set; } = false;
    }
}
