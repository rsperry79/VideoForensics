using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Report analyzing device health metrics and identifying signal anomalies.</summary>
    public class SignalAnomalyReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<AnomalyFindings> AnomaliesByDevice { get; set; } = new List<AnomalyFindings>();
        public IReadOnlyList<JammingSummaryEntry> JammingByDevice { get; set; } = new List<JammingSummaryEntry>();

        public class JammingSummaryEntry
        {
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public int IncidentCount { get; set; }
            public double TotalJammedDurationMinutes { get; set; }
            public double AverageDegradationDb { get; set; }
            public double MaxDegradationDb { get; set; }
            public DateTime? FirstIncidentUtc { get; set; }
            public DateTime? LastIncidentUtc { get; set; }
        }

        public class AnomalyFindings
        {
            public Guid DeviceId { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public IReadOnlyList<SignalAnomaly> Anomalies { get; set; } = new List<SignalAnomaly>();
        }

        public class SignalAnomaly
        {
            public DateTime OccurredAtUtc { get; set; }
            public string AnomalyType { get; set; } = string.Empty; // e.g., "DegradedSignal", "ConnectionLoss"
            public string? Description { get; set; }
            public int? RssiValue { get; set; }
        }
    }
}
