using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
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
        public List<SignalAnomalyFinding> AnomalousEvents { get; set; } = [];
        public List<JammingIncident> DetectedJammingIncidents { get; set; } = [];
        public Dictionary<string, RssiStatistics> PerCameraBaselineStatistics { get; set; } = [];
        public List<CameraSignalProfile> CameraProfiles { get; set; } = [];
        public string? RiskAssessment { get; set; }
        public string? Recommendations { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = [];

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
        public string? SigningCertificateThumbprint { get; set; }
        public TimeSyncValidation? DeviceTimeSyncStatus { get; set; }
    }
}
