namespace VideoForensics.Client.Core.Tools
{
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Data.Common.Entities;

    public class JammingToolsOrchestrator
    {
        private readonly ILogger<JammingToolsOrchestrator> _logger;
        private readonly IJammingRepository _jammingRepository;
        private readonly IDeviceHealthSnapshotRepository _healthSnapshotRepository;

        // A drop of at least this many dB below the device's own baseline RSSI is treated as
        // "degraded" for a single reading. Matches the playbook in JammingAnalysisResource
        // ("many dB below baseline", not a routine few-dB dip).
        private const double DegradationThresholdDb = 8.0;

        // Minimum consecutive degraded readings required to record an incident at all — a single
        // bad reading is noise (per the playbook), not a candidate incident.
        private const int MinConsecutiveReadingsForIncident = 2;

        public JammingToolsOrchestrator(
            ILogger<JammingToolsOrchestrator> logger,
            IJammingRepository jammingRepository,
            IDeviceHealthSnapshotRepository healthSnapshotRepository)
        {
            _logger = logger;
            _jammingRepository = jammingRepository;
            _healthSnapshotRepository = healthSnapshotRepository;
        }

        public async Task<(bool Success, string Message, JammingStatsSummary? Stats)> RunJammingDetectionNotificationAsync(
            Guid deviceId,
            string? detectionNotes = null,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Recording jamming detection notification for device {deviceId}", deviceId);
                await _jammingRepository.RecomputeStatsAsync(deviceId, ct);
                var stats = await _jammingRepository.GetStatsAsync(deviceId, ct);

                var message = stats?.IncidentCount > 0
                    ? $"Jamming detection summary: {stats.IncidentCount} incident(s), {stats.TotalJammedDurationMinutes:F1} minutes total duration, avg degradation {stats.AverageDegradationDb:F1} dB"
                    : "No jamming incidents recorded for this device";

                return (true, message, stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve jamming detection summary for device {deviceId}", deviceId);
                return (false, $"Failed to retrieve detection summary: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, JammingIncidentRecord? Record)> RecordJammingIncidentAsync(
            Guid deviceId,
            DateTime startUtc,
            DateTime endUtc,
            int affectedEventCount,
            double averageDegradationDb,
            JammingConfidenceLevel confidence,
            string? notes,
            CancellationToken ct = default)
        {
            try
            {
                if (startUtc >= endUtc)
                    return (false, "Start time must be before end time", null);

                if (averageDegradationDb < 0)
                    return (false, "Average degradation must be non-negative", null);

                var record = new JammingIncidentRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    AffectedEventCount = affectedEventCount,
                    AverageDegradationDb = averageDegradationDb,
                    Confidence = confidence,
                    DetectedAtUtc = DateTime.UtcNow,
                    Notes = notes,
                    Source = JammingIncidentSource.ManuallyRecorded
                };

                var persisted = await _jammingRepository.UpsertIncidentAsync(record, ct);
                await _jammingRepository.RecomputeStatsAsync(deviceId, ct);
                _logger.LogInformation("Recorded manual jamming incident for device {deviceId}", deviceId);

                return (true, "Jamming incident recorded successfully", persisted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record jamming incident for device {deviceId}", deviceId);
                return (false, $"Failed to record incident: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, JammingStatsSummary? Stats)> GetJammingStatsAsync(
            Guid deviceId,
            CancellationToken ct = default)
        {
            try
            {
                var stats = await _jammingRepository.GetStatsAsync(deviceId, ct);
                return stats != null
                    ? (true, stats)
                    : (true, new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 0 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve jamming stats for device {deviceId}", deviceId);
                return (false, null);
            }
        }

        public async Task<(bool Success, IReadOnlyList<JammingIncidentRecord>? Incidents)> GetJammingIncidentsAsync(
            Guid? deviceId = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            CancellationToken ct = default)
        {
            try
            {
                var incidents = await _jammingRepository.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct);
                return (true, incidents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve jamming incidents");
                return (false, null);
            }
        }

        /// <summary>
        /// Unified jamming analysis: detects incidents from captured RSSI history, persists them,
        /// recomputes device stats, and returns everything in one call.
        ///
        /// Detection runs against DeviceHealthSnapshot rows (real RSSI readings captured once per
        /// download batch — see RingMediaDownloadService.CaptureDeviceHealthSnapshotAsync). Ring's
        /// /doorbots/history endpoint does not return RSSI on individual historical events (the
        /// embedded Doorbot stub's Health is always null there), so per-event RSSI does not exist to
        /// analyze; the health-snapshot timeline is the actual signal-strength data this system has.
        /// </summary>
        public async Task<JammingAnalysisReport> AnalyzeJammingAsync(
            Guid deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            try
            {
                if (fromUtc >= toUtc)
                    return new JammingAnalysisReport
                    {
                        Success = false,
                        ErrorMessage = "Start time must be before end time"
                    };

                _logger.LogInformation("Starting jamming analysis for device {deviceId} from {fromUtc} to {toUtc}",
                    deviceId, fromUtc, toUtc);

                var detectedCount = await DetectAndPersistIncidentsAsync(deviceId, fromUtc, toUtc, ct);

                await _jammingRepository.RecomputeStatsAsync(deviceId, ct);

                var stats = await _jammingRepository.GetStatsAsync(deviceId, ct);
                var incidents = await _jammingRepository.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct);

                return new JammingAnalysisReport
                {
                    Success = true,
                    DeviceId = deviceId,
                    AnalysisWindowUtc = (fromUtc, toUtc),
                    Summary = stats ?? new JammingStatsSummary { DeviceId = deviceId, IncidentCount = 0 },
                    Incidents = incidents ?? new List<JammingIncidentRecord>(),
                    AnalyzedAtUtc = DateTime.UtcNow,
                    Message = stats?.IncidentCount > 0
                        ? $"Found {stats.IncidentCount} incident(s) ({detectedCount} newly detected this run): " +
                          $"{stats.TotalJammedDurationMinutes:F1} min total, avg degradation {stats.AverageDegradationDb:F1} dB. " +
                          $"High: {stats.HighConfidenceCount}, Medium: {stats.MediumConfidenceCount}, Low: {stats.LowConfidenceCount}"
                        : "No jamming incidents detected in this device's health-snapshot history"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Jamming analysis failed for device {deviceId}", deviceId);
                return new JammingAnalysisReport
                {
                    Success = false,
                    ErrorMessage = $"Analysis failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Scans the device's health-snapshot history for sustained RSSI degradation relative to its
        /// own baseline, persists any runs found as auto-detected JammingIncidentRecords (upserted by
        /// StartUtc so re-running analysis over the same window doesn't create duplicates), and
        /// returns how many incidents were found this run.
        /// </summary>
        private async Task<int> DetectAndPersistIncidentsAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var history = await _healthSnapshotRepository.GetHistoryAsync(deviceId, ct);

            var readings = history
                .Where(s => s.Rssi.HasValue && s.CapturedAtUtc >= fromUtc && s.CapturedAtUtc <= toUtc)
                .OrderBy(s => s.CapturedAtUtc)
                .ToList();

            // Baseline needs enough readings to be meaningful; a device with only 1-2 snapshots in
            // the window has no established "normal" to compare against.
            if (readings.Count < 3)
            {
                _logger.LogInformation("Insufficient RSSI history for device {deviceId} in window ({Count} reading(s)); skipping detection",
                    deviceId, readings.Count);
                return 0;
            }

            var baselineRssi = Median(readings.Select(r => (double)r.Rssi!.Value));

            var existingIncidents = await _jammingRepository.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct);
            var alreadyDetectedStarts = existingIncidents
                .Where(i => i.Source == JammingIncidentSource.AutoDetected)
                .Select(i => i.StartUtc)
                .ToHashSet();

            var detected = 0;
            var runStart = -1;

            for (var i = 0; i <= readings.Count; i++)
            {
                var isDegraded = i < readings.Count && (baselineRssi - readings[i].Rssi!.Value) >= DegradationThresholdDb;

                if (isDegraded && runStart == -1)
                {
                    runStart = i;
                }
                else if (!isDegraded && runStart != -1)
                {
                    var runLength = i - runStart;
                    if (runLength >= MinConsecutiveReadingsForIncident)
                    {
                        var runReadings = readings.GetRange(runStart, runLength);
                        var incidentStart = runReadings[0].CapturedAtUtc;

                        if (!alreadyDetectedStarts.Contains(incidentStart))
                        {
                            var avgDegradation = baselineRssi - runReadings.Average(r => r.Rssi!.Value);

                            await _jammingRepository.UpsertIncidentAsync(new JammingIncidentRecord
                            {
                                Id = Guid.NewGuid(),
                                DeviceId = deviceId,
                                StartUtc = incidentStart,
                                EndUtc = runReadings[^1].CapturedAtUtc,
                                AffectedEventCount = runLength,
                                AverageDegradationDb = avgDegradation,
                                Confidence = ClassifyConfidence(runLength, avgDegradation),
                                DetectedAtUtc = DateTime.UtcNow,
                                Notes = $"Auto-detected: baseline RSSI {baselineRssi:F1} dBm, {runLength} consecutive degraded reading(s)",
                                Source = JammingIncidentSource.AutoDetected
                            }, ct);

                            detected++;
                        }
                    }

                    runStart = -1;
                }
            }

            _logger.LogInformation("Jamming detection for device {deviceId}: baseline={Baseline:F1} dBm, {ReadingCount} reading(s) scanned, {Detected} new incident(s)",
                deviceId, baselineRssi, readings.Count, detected);

            return detected;
        }

        /// <summary>Classifies confidence from run length (sustained-ness) and average degradation magnitude, per the JammingAnalysisResource playbook.</summary>
        private static JammingConfidenceLevel ClassifyConfidence(int runLength, double avgDegradationDb)
        {
            if (runLength >= 5 && avgDegradationDb >= 20)
                return JammingConfidenceLevel.Definite;
            if (runLength >= 3 && avgDegradationDb >= 15)
                return JammingConfidenceLevel.High;
            if (runLength >= 2 && avgDegradationDb >= 10)
                return JammingConfidenceLevel.Medium;
            return JammingConfidenceLevel.Low;
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }

    /// <summary>Unified jamming analysis result: all findings in one response.</summary>
    public class JammingAnalysisReport
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid DeviceId { get; set; }
        public (DateTime From, DateTime To) AnalysisWindowUtc { get; set; }
        public JammingStatsSummary Summary { get; set; } = new();
        public IReadOnlyList<JammingIncidentRecord> Incidents { get; set; } = new List<JammingIncidentRecord>();
        public DateTime AnalyzedAtUtc { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>Summary of findings for quick reference.</summary>
        public override string ToString()
        {
            if (!Success)
                return $"Analysis failed: {ErrorMessage}";

            if (Summary.IncidentCount == 0)
                return "No jamming incidents detected";

            var high = Summary.HighConfidenceCount;
            var medium = Summary.MediumConfidenceCount;
            var low = Summary.LowConfidenceCount;

            return $"{Summary.IncidentCount} incident(s): High={high}, Medium={medium}, Low={low} | " +
                   $"{Summary.TotalJammedDurationMinutes:F0}min duration | " +
                   $"Max degradation: {Summary.MaxDegradationDb:F1} dB";
        }
    }
}
