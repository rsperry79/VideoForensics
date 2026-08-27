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
