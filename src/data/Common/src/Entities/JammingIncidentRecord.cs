namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A raw jamming or signal interference incident record for a device.</summary>
    public class JammingIncidentRecord
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int AffectedEventCount { get; set; }
        public double AverageDegradationDb { get; set; }
        public JammingConfidenceLevel Confidence { get; set; }
        public DateTime DetectedAtUtc { get; set; }
        public string? Notes { get; set; }
        public JammingIncidentSource Source { get; set; }
        /// <summary>Forensic case auto-created for this incident; null until one exists.</summary>
        public Guid? CaseId { get; set; }
    }

    public enum JammingConfidenceLevel
    {
        Low,
        Medium,
        High,
        Definite
    }

    public enum JammingIncidentSource
    {
        AutoDetected,
        ManuallyRecorded
    }
}
