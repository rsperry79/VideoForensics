using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Forensics.Models;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Forensics;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>
    /// MCP tools for jamming/RF-interference detection and its persisted incident/summary tables.
    /// See the "videoforensics://instructions/jamming-analysis" resource for guidance on how and
    /// when to use these.
    /// </summary>
    [McpServerToolType]
    public static class JammingTools
    {
        [McpServerTool, Description("Runs jamming/signal-interference detection over a device's raw provider events for a date range, persists any detected incidents and recomputes the device's summary stats, and returns both. See the 'videoforensics://instructions/jamming-analysis' resource before interpreting results.")]
        public static async Task<object> RunJammingDetection(
            IDeviceRepository deviceRepository,
            ISessionProvider sessionProvider,
            ISignalAnomalyDetector signalAnomalyDetector,
            IJammingRepository jammingRepository,
            ILogger<object> logger,
            [Description("Data-layer device Guid to analyze")] Guid deviceId,
            [Description("Start of the analysis window (UTC)")] DateTime fromUtc,
            [Description("End of the analysis window (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            var device = await deviceRepository.GetAsync(deviceId, cancellationToken);
            if (device == null)
            {
                throw new InvalidOperationException($"Device {deviceId} not found.");
            }

            var session = sessionProvider.GetSession();
            if (session == null)
            {
                throw new InvalidOperationException("Not authenticated. Call AccountTools.Authenticate first.");
            }

            if (!long.TryParse(device.ProviderDeviceId, out var doorbotId))
            {
                throw new InvalidOperationException($"Device {deviceId} has a non-numeric provider device id ('{device.ProviderDeviceId}') and cannot be matched against Ring history events.");
            }

            var historyEvents = await session.GetDoorbotsHistory(fromUtc, toUtc, doorbotId);

            IEnumerable<JammingIncident> detected;
            try
            {
                detected = await signalAnomalyDetector.DetectJammingAsync(historyEvents ?? new List<DoorbotHistoryEvent>());
            }
            catch (NotImplementedException)
            {
                logger.LogWarning("ISignalAnomalyDetector.DetectJammingAsync is not yet implemented upstream (src/core/forensics)");
                return new
                {
                    Error = "Jamming detection is not yet implemented in the underlying signal analysis engine (ISignalAnomalyDetector.DetectJammingAsync). Use JammingTools.RecordJammingIncident to record incidents manually in the meantime.",
                    EventsExamined = historyEvents?.Count ?? 0
                };
            }

            var persisted = new List<JammingIncidentRecord>();
            var nowUtc = DateTime.UtcNow;
            foreach (var incident in detected)
            {
                var record = new JammingIncidentRecord
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    StartUtc = incident.IncidentStartTime,
                    EndUtc = incident.IncidentEndTime,
                    AffectedEventCount = incident.AffectedEventCount,
                    AverageDegradationDb = incident.AverageDegradation,
                    Confidence = MapConfidence(incident.ConfidenceLevel),
                    DetectedAtUtc = nowUtc,
                    Notes = incident.IncidentDescription,
                    Source = JammingIncidentSource.AutoDetected
                };

                persisted.Add(await jammingRepository.UpsertIncidentAsync(record, cancellationToken));
            }

            var summary = await jammingRepository.RecomputeStatsAsync(deviceId, cancellationToken);

            return new
            {
                DeviceId = deviceId,
                EventsExamined = historyEvents?.Count ?? 0,
                IncidentsDetected = persisted.Count,
                Incidents = persisted,
                Summary = summary
            };
        }

        [McpServerTool, Description("Manually records a jamming/interference incident for a device (e.g. to correct or supplement an auto-detected result), then recomputes the device's summary stats.")]
        public static async Task<JammingStatsSummary> RecordJammingIncident(
            IJammingRepository jammingRepository,
            IDeviceRepository deviceRepository,
            [Description("Data-layer device Guid")] Guid deviceId,
            [Description("Start of the incident (UTC)")] DateTime startUtc,
            [Description("End of the incident (UTC); must not be before startUtc")] DateTime endUtc,
            [Description("Number of events affected by the incident")] int affectedEventCount,
            [Description("Average RSSI degradation, in dB, observed during the incident")] double averageDegradationDb,
            [Description("Confidence level: Low, Medium, High, or Definite")] JammingConfidenceLevel confidence,
            CancellationToken cancellationToken,
            [Description("Optional free-text notes about this incident")] string? notes = null)
        {
            if (endUtc < startUtc)
            {
                throw new ArgumentException("endUtc must not be before startUtc.");
            }
            if (affectedEventCount < 0)
            {
                throw new ArgumentException("affectedEventCount must not be negative.");
            }

            var device = await deviceRepository.GetAsync(deviceId, cancellationToken);
            if (device == null)
            {
                throw new InvalidOperationException($"Device {deviceId} not found.");
            }

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

            await jammingRepository.UpsertIncidentAsync(record, cancellationToken);
            return await jammingRepository.RecomputeStatsAsync(deviceId, cancellationToken);
        }

        [McpServerTool, Description("Reads the persisted jamming/interference summary statistics for one device, or all devices if deviceId is omitted.")]
        public static async Task<IReadOnlyList<JammingStatsSummary>> GetJammingStats(
            IJammingRepository jammingRepository,
            CancellationToken cancellationToken,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId = null)
        {
            if (deviceId.HasValue)
            {
                var stats = await jammingRepository.GetStatsAsync(deviceId.Value, cancellationToken);
                return stats == null ? Array.Empty<JammingStatsSummary>() : new[] { stats };
            }

            return await jammingRepository.ListStatsAsync(cancellationToken);
        }

        [McpServerTool, Description("Lists the raw persisted jamming/interference incident records, optionally filtered by device and/or date range.")]
        public static async Task<IReadOnlyList<JammingIncidentRecord>> GetJammingIncidents(
            IJammingRepository jammingRepository,
            CancellationToken cancellationToken,
            [Description("Restrict to a single device (data-layer Guid), or omit for all devices")] Guid? deviceId = null,
            [Description("Only incidents starting on/after this UTC time, or omit for no lower bound")] DateTime? fromUtc = null,
            [Description("Only incidents ending on/before this UTC time, or omit for no upper bound")] DateTime? toUtc = null)
        {
            return await jammingRepository.ListIncidentsAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }

        private static JammingConfidenceLevel MapConfidence(JammingConfidence confidence) => confidence switch
        {
            JammingConfidence.Low => JammingConfidenceLevel.Low,
            JammingConfidence.Medium => JammingConfidenceLevel.Medium,
            JammingConfidence.High => JammingConfidenceLevel.High,
            JammingConfidence.Definite => JammingConfidenceLevel.Definite,
            _ => JammingConfidenceLevel.Low
        };
    }
}
