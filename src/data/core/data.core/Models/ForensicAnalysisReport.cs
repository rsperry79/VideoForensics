using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Comprehensive forensic analysis report combining evidence, anomalies, and access control data.</summary>
    public class ForensicAnalysisReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<MediaItem> EvidenceItems { get; set; } = [];
        public IReadOnlyList<DeviceHealthSnapshot> AnomalousHealthSnapshots { get; set; } = [];
        public IReadOnlyList<ActionLogEntry> SignificantActions { get; set; } = [];
        public string? Summary { get; set; }
    }
}
