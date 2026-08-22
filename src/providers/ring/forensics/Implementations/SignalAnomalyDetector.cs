using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Forensics.Models;
using VideoForensics.Providers.Ring.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring.Forensics.Implementations
{
    /// <summary>
    /// Stub implementation of signal anomaly detection.
    /// To be completed with RSSI analysis logic for tamper/jamming detection.
    /// </summary>
    internal class SignalAnomalyDetector : ISignalAnomalyDetector
    {
        public Task<RssiStatistics> CalculateRssiStatisticsAsync(
            string deviceId,
            IEnumerable<DoorbotHistoryEvent> events)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<CameraSignalProfile>> AnalyzeCameraSignalsAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            ForensicsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<SignalAnomalyFinding>> DetectSignalAnomaliesAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            Dictionary<string, RssiStatistics>? statsPerCamera = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<JammingIncident>> DetectJammingAsync(
            IEnumerable<DoorbotHistoryEvent> events)
        {
            throw new NotImplementedException();
        }

        public Task<TimeSyncValidation> ValidateDeviceTimeAsync(
            string deviceId,
            IEnumerable<DoorbotHistoryEvent> events)
        {
            throw new NotImplementedException();
        }

        public Task<SignalAnomalyReport> GetSignalAnomalyReportAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            IEnumerable<SignalAnomalyFinding> anomalies,
            IEnumerable<JammingIncident> jammingIncidents)
        {
            throw new NotImplementedException();
        }
    }
}
