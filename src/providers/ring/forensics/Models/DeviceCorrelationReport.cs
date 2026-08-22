using System;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Forensics.Models
{
    public class DeviceCorrelationReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public List<string> DeviceIds { get; set; } = new();
        public List<SyncedAnomalyEvent> SynchronizedAnomalies { get; set; } = new();
        public double SuspicionScore { get; set; }
        public string? IncidentPattern { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        public string? DigitalSignature { get; set; }
        public DateTime? ReportSignedAt { get; set; }
        public string? SignedByOfficer { get; set; }
    }

    public class SyncedAnomalyEvent
    {
        public DateTime OccurredAt { get; set; }
        public List<string> AffectedDeviceIds { get; set; } = new();
        public string? AnomalyType { get; set; }
        public TimeSpan TimestampVariance { get; set; }
        public string? Description { get; set; }
        public double CoordinationScore { get; set; }
    }
}
