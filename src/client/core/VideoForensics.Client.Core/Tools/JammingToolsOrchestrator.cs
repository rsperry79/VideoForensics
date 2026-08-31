namespace VideoForensics.Client.Core.Tools
{
    using Microsoft.Extensions.Logging;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Data.Common.Entities;

    public class JammingToolsOrchestrator
    {
        private readonly ILogger<JammingToolsOrchestrator> _logger;
        private readonly IJammingRepository _jammingRepository;

        public JammingToolsOrchestrator(
            ILogger<JammingToolsOrchestrator> logger,
            IJammingRepository jammingRepository)
        {
            _logger = logger;
            _jammingRepository = jammingRepository;
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

        /// <summary>Unified jamming analysis: detect + analyze + summarize in one call.</summary>
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

                _logger.LogInformation("Starting unified jamming analysis for device {deviceId} from {fromUtc} to {toUtc}",
                    deviceId, fromUtc, toUtc);

                // Recompute stats (includes detection)
                await _jammingRepository.RecomputeStatsAsync(deviceId, ct);

                // Get comprehensive results
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
                        ? $"Found {stats.IncidentCount} incident(s): {stats.TotalJammedDurationMinutes:F1} min total, " +
                          $"avg degradation {stats.AverageDegradationDb:F1} dB. " +
                          $"High: {stats.HighConfidenceCount}, Medium: {stats.MediumConfidenceCount}, Low: {stats.LowConfidenceCount}"
                        : "No jamming incidents detected in this device's history"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unified jamming analysis failed for device {deviceId}", deviceId);
                return new JammingAnalysisReport
                {
                    Success = false,
                    ErrorMessage = $"Analysis failed: {ex.Message}"
                };
            }
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
