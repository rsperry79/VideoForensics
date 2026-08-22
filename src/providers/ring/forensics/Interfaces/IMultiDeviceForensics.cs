using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Forensics.Models;
using VideoForensics.Providers.Ring.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Analyzes forensic evidence across multiple devices to identify coordinated attacks.
    /// In DV cases, offenders may attempt synchronized tampering across all cameras.
    /// </summary>
    public interface IMultiDeviceForensics
    {
        /// <summary>
        /// Analyze signal anomalies across multiple devices for synchronized/coordinated patterns.
        /// </summary>
        Task<DeviceCorrelationReport> AnalyzeMultipleDevicesAsync(
            IEnumerable<string> deviceIds,
            IEnumerable<SignalAnomalyReport> perDeviceReports);

        /// <summary>
        /// Detect simultaneous signal degradation or loss across devices.
        /// </summary>
        Task<IEnumerable<SyncedAnomalyEvent>> DetectSynchronizedAnomaliesAsync(
            IEnumerable<string> deviceIds,
            TimeSpan coincidenceWindow);

        /// <summary>
        /// Calculate correlation score between anomalies on different devices.
        /// </summary>
        Task<double> CalculateAnomalyCorrelationAsync(
            string deviceId1,
            string deviceId2,
            DateTime startTime,
            DateTime endTime);

        /// <summary>
        /// Get baseline correlations to identify when behavior is unusual.
        /// </summary>
        Task<BaselineCorrelation> GetBaselineCorrelationAsync(
            IEnumerable<string> deviceIds,
            int baselineDays = 30);
    }

    public class BaselineCorrelation
    {
        public List<string> DeviceIds { get; set; } = new();
        public double AverageCorrelation { get; set; }
        public double StandardDeviation { get; set; }
        public double MinObservedCorrelation { get; set; }
        public double MaxObservedCorrelation { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
        public int DataPointsUsed { get; set; }
    }
}
