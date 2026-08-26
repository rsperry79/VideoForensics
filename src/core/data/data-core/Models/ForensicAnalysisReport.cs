using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Comprehensive forensic analysis report combining evidence, anomalies, and access control data.</summary>
    public class ForensicAnalysisReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<MediaItem> EvidenceItems { get; set; } = new List<MediaItem>();
        public IReadOnlyList<DeviceHealthSnapshot> AnomalousHealthSnapshots { get; set; } = new List<DeviceHealthSnapshot>();
        public IReadOnlyList<ActionLogEntry> SignificantActions { get; set; } = new List<ActionLogEntry>();
        public string? Summary { get; set; }
    }
}
