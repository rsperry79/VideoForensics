using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    /// <summary>
    /// Contains forensic metadata extracted from a Ring device event,
    /// suitable for legal proceedings and investigative analysis.
    /// </summary>
    public class EvidenceMetadata
    {
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ExtractionTimestamp { get; set; } = DateTime.UtcNow;
        public string? SourceDeviceId { get; set; }
        public DateTime? EventTimestamp { get; set; }
        public string? EventType { get; set; }
        public Dictionary<string, object> ExtractedData { get; set; } = new();
        public Dictionary<string, string> Checksums { get; set; } = new();
        public string? ExtractionHandler { get; set; }
        public string? Notes { get; set; }
    }
}
