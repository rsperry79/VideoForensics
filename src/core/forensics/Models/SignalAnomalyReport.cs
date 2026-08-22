using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models.Reports
{
    /// <summary>
    /// Strongly-typed report of signal anomalies (tampering, jamming, interference).
    /// Client application decides how to persist this report.
    /// </summary>
    public class SignalAnomalyReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalEventsAnalyzed { get; set; }
        public List<SignalAnomalyFinding> AnomalousEvents { get; set; } = new();
        public List<JammingIncident> DetectedJammingIncidents { get; set; } = new();
        public Dictionary<string, RssiStatistics> PerCameraBaselineStatistics { get; set; } = new();
        public List<CameraSignalProfile> CameraProfiles { get; set; } = new();
        public string? RiskAssessment { get; set; }
        public string? Recommendations { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
        public TimeSyncValidation? DeviceTimeSyncStatus { get; set; }
    }
}
