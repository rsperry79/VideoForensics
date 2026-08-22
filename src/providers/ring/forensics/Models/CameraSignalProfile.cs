using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Forensics.Models
{
    /// <summary>
    /// Per-camera signal strength profile used to detect tamper/jamming incidents.
    /// </summary>
    public class CameraSignalProfile
    {
        public string CameraId { get; set; } = string.Empty;
        public RssiStatistics? BaselineStatistics { get; set; }
        public bool AnomaliesDetected { get; set; }
        public int AnomalousEventCount { get; set; }
        public double AnomalyPercentage { get; set; }
        public List<string> FlagsForReview { get; set; } = new();
    }
}
