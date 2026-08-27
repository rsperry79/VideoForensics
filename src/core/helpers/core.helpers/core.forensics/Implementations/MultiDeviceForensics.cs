using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Forensics.Models;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of multi-device forensic analysis.
    /// To be completed with actual correlation detection logic.
    /// </summary>
    internal class MultiDeviceForensics : IMultiDeviceForensics
    {
        public Task<DeviceCorrelationReport> AnalyzeMultipleDevicesAsync(
            IEnumerable<string> deviceIds,
            IEnumerable<SignalAnomalyReport> perDeviceReports)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<SyncedAnomalyEvent>> DetectSynchronizedAnomaliesAsync(
            IEnumerable<string> deviceIds,
            TimeSpan coincidenceWindow)
        {
            throw new NotImplementedException();
        }

        public Task<double> CalculateAnomalyCorrelationAsync(
            string deviceId1,
            string deviceId2,
            DateTime startTime,
            DateTime endTime)
        {
            throw new NotImplementedException();
        }

        public Task<BaselineCorrelation> GetBaselineCorrelationAsync(
            IEnumerable<string> deviceIds,
            int baselineDays = 30)
        {
            throw new NotImplementedException();
        }
    }
}
