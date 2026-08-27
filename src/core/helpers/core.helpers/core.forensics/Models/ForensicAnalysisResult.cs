using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    public class ForensicAnalysisResult
    {
        public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
        public string EvidenceId { get; set; } = string.Empty;
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
        public string? Finding { get; set; }
        public AnalysisSeverity Severity { get; set; } = AnalysisSeverity.Info;
        public List<string> Tags { get; set; } = new();
        public string? Recommendation { get; set; }
        public Dictionary<string, object> AnalysisData { get; set; } = new();
    }

    public enum AnalysisSeverity
    {
        Info,
        Warning,
        Critical
    }

    public enum ReportFormat
    {
        Summary,
        Detailed,
        Legal,
        Json
    }
}
