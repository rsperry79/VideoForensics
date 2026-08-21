using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Entities;
using Ring.Api.Forensics.Models;
using Ring.Api.Forensics.Models.Reports;

namespace Ring.Api.Forensics.Implementations
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
