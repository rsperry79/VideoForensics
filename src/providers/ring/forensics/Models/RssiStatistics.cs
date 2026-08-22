using System;

namespace VideoForensics.Providers.Ring.Forensics.Models
{
    /// <summary>
    /// Statistical analysis of RSSI (signal strength) data for a device.
    /// Used to establish baselines and detect signal anomalies.
    /// </summary>
    public class RssiStatistics
    {
        public string DeviceId { get; set; } = string.Empty;
        public double MedianRssi { get; set; }
        public double StandardDeviation { get; set; }
        public double MinRssi { get; set; }
        public double MaxRssi { get; set; }
        public int SampleCount { get; set; }
        public DateTime AnalyzedFrom { get; set; }
        public DateTime AnalyzedTo { get; set; }
    }
}
