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
    }
}
