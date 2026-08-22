using System;
using System.Collections.Generic;

namespace VideoForensics.Forensics.Models
{
    /// <summary>
    /// Represents a detected jamming or signal interference incident,
    /// characterized by sustained signal degradation over time.
    /// </summary>
    public class JammingIncident
    {
        public string IncidentId { get; set; } = Guid.NewGuid().ToString();
        public string DeviceId { get; set; } = string.Empty;
        public DateTime IncidentStartTime { get; set; }
        public DateTime IncidentEndTime { get; set; }
        public TimeSpan Duration => IncidentEndTime - IncidentStartTime;
        public int AffectedEventCount { get; set; }
        public double AverageDegradation { get; set; }
        public List<string> AffectedEventIds { get; set; } = new();
        public string? IncidentDescription { get; set; }
        public JammingConfidence ConfidenceLevel { get; set; }
    }

    public enum JammingConfidence
    {
        Low,
        Medium,
        High,
        Definite
    }
}
