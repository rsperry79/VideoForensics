using System;

namespace Ring.Api.Forensics.Models
{
    /// <summary>
    /// Represents an event flagged for manual review due to signal anomalies.
    /// Indicates potential tampering, jamming, or signal interference.
    /// </summary>
    public class SignalAnomalyFinding
    {
        public string EventId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime EventTimestamp { get; set; }
        public double? ObservedRssi { get; set; }
        public double? ExpectedRssi { get; set; }
        public double DeviationFromMedian { get; set; }
        public int StandardDeviationsFromMean { get; set; }
        public SignalAnomalyType AnomalyType { get; set; }
        public string? Description { get; set; }
        public ReviewPriority Priority { get; set; }
    }

    public enum SignalAnomalyType
    {
        ExtremelyWeakSignal,
        UnusualStrengthVariance,
        SuddenDrop,
        SustainedDegradation,
        UnexpectedStrength,
        NoSignal
    }

    public enum ReviewPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
