using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Forensics;
using VideoForensics.Forensics.Models;
using VideoForensics.Forensics.Models.Reports;

namespace VideoForensics.Providers.Ring.Forensics
{
    /// <summary>
    /// Analyzes signal strength (RSSI) data to detect anomalies indicative of
    /// device tampering, jamming, or signal interference. Critical for identifying
    /// malicious interference with Ring devices.
    /// Reports are returned as strongly-typed DTOs; client application
    /// decides how to persist them (JSON, database, etc).
    /// </summary>
    public interface ISignalAnomalyDetector
    {
        /// <summary>
        /// Calculates statistical properties (standard deviation and median) of RSSI
        /// for a specific device across a time period.
        /// </summary>
        /// <param name="deviceId">The device to analyze.</param>
        /// <param name="events">Events containing RSSI data.</param>
        /// <returns>Signal statistics for the device.</returns>
        Task<RssiStatistics> CalculateRssiStatisticsAsync(string deviceId, IEnumerable<DoorbotHistoryEvent> events);

        /// <summary>
        /// Analyzes RSSI data per camera to establish baseline and detect deviations.
        /// </summary>
        /// <param name="events">Events with RSSI measurements.</param>
        /// <param name="options">Configuration for analysis thresholds.</param>
        /// <returns>Per-camera signal statistics and anomaly flags.</returns>
        Task<IEnumerable<CameraSignalProfile>> AnalyzeCameraSignalsAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            ForensicsOptions? options = null);

        /// <summary>
        /// Identifies events with abnormal RSSI values that may indicate tampering,
        /// jamming, or signal interference.
        /// </summary>
        /// <param name="events">Events to scan for signal anomalies.</param>
        /// <param name="statsPerCamera">Pre-calculated statistics per camera device.</param>
        /// <returns>Events flagged for manual review with anomaly details.</returns>
        Task<IEnumerable<SignalAnomalyFinding>> DetectSignalAnomaliesAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            Dictionary<string, RssiStatistics>? statsPerCamera = null);

        /// <summary>
        /// Detects potential jamming attacks based on sustained signal degradation.
        /// </summary>
        /// <param name="events">Time-ordered events to analyze.</param>
        /// <returns>Jamming incidents with timeframes and affected devices.</returns>
        Task<IEnumerable<JammingIncident>> DetectJammingAsync(IEnumerable<DoorbotHistoryEvent> events);

        /// <summary>
        /// Validates device time synchronization by detecting timestamp anomalies.
        /// Used to detect if an attacker modified device system time to hide tampering.
        /// </summary>
        Task<TimeSyncValidation> ValidateDeviceTimeAsync(string deviceId, IEnumerable<DoorbotHistoryEvent> events);

        /// <summary>
        /// Retrieves a strongly-typed signal anomaly report with all findings.
        /// Includes RSSI statistics, anomalies, jamming incidents, and risk assessment.
        /// Client application decides how to persist this report (JSON, database, etc).
        /// </summary>
        /// <param name="events">Events analyzed to generate the report.</param>
        /// <param name="anomalies">Signal anomalies detected.</param>
        /// <param name="jammingIncidents">Jamming incidents detected.</param>
        /// <returns>Strongly-typed signal anomaly report.</returns>
        Task<SignalAnomalyReport> GetSignalAnomalyReportAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            IEnumerable<SignalAnomalyFinding> anomalies,
            IEnumerable<JammingIncident> jammingIncidents);
    }
}
