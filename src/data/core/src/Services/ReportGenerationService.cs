using Microsoft.Extensions.Logging;

using System.Text.Json;

using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
// Aliased: importing the whole providers-common namespace makes `Device` ambiguous with Data.Common.Entities.Device.
using INotificationDispatcher = VideoForensics.Core.Providers.Common.Contracts.INotificationDispatcher;
using NoticeSeverity = VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity;
using NotificationAudience = VideoForensics.Core.Providers.Common.Contracts.NotificationAudience;
using NotificationEvent = VideoForensics.Core.Providers.Common.Contracts.NotificationEvent;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for generating forensic analysis reports from stored evidence data.</summary>
    internal class ReportGenerationService : IReportGenerationService
    {
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDownloadEventRepository _downloadEventRepository;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IJammingRepository _jammingRepository;
        private readonly IIntegrityRecordRepository _integrityRecordRepository;
        private readonly ILogger<ReportGenerationService> _logger;
        private readonly IStorageLocationProvider _storageLocationProvider;
        private readonly INotificationDispatcher? _notificationDispatcher;

        // Chain-failure alert throttle. Per service instance, so one circuit's fan-out of per-device reports alerts once.
        private readonly object _chainAlertGate = new();
        private DateTime _lastChainAlertUtc = DateTime.MinValue;
        private static readonly TimeSpan ChainAlertWindow = TimeSpan.FromHours(1);

        public ReportGenerationService(
            IMediaItemRepository mediaItemRepository,
            IDeviceRepository deviceRepository,
            IDownloadEventRepository downloadEventRepository,
            IActionLogRepository actionLogRepository,
            IEventRepository eventRepository,
            IJammingRepository jammingRepository,
            IIntegrityRecordRepository integrityRecordRepository,
            ILogger<ReportGenerationService> logger,
            IStorageLocationProvider storageLocationProvider,
            INotificationDispatcher? notificationDispatcher = null)
        {
            _notificationDispatcher = notificationDispatcher;
            _mediaItemRepository = mediaItemRepository;
            _deviceRepository = deviceRepository;
            _downloadEventRepository = downloadEventRepository;
            _actionLogRepository = actionLogRepository;
            _eventRepository = eventRepository;
            _jammingRepository = jammingRepository;
            _integrityRecordRepository = integrityRecordRepository;
            _logger = logger;
            _storageLocationProvider = storageLocationProvider;
        }

        public async Task<EvidenceReviewReport> BuildEvidenceReviewAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var report = new EvidenceReviewReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = fromUtc,
                ReportToUtc = toUtc
            };

            try
            {
                IReadOnlyList<MediaItem> items;
                if (deviceId.HasValue)
                {
                    items = await _mediaItemRepository.GetByDeviceAndDateRangeAsync(deviceId.Value, fromUtc, toUtc, ct);
                }
                else
                {
                    IReadOnlyList<MediaItem> allItems = await _mediaItemRepository.ListAsync(ct);
                    items = allItems.Where(m => m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList();
                }

                IReadOnlyList<IntegrityRecord> integrityRecords = await _integrityRecordRepository.GetLatestByMediaItemIdsAsync(
                    items.Select(m => m.Id), ct);

                // Map each download event to its provider event id so media can be matched to event rows.
                var providerEventIdByDownloadEventId = new Dictionary<Guid, string>();
                foreach (Guid deviceIdForItems in items.Select(m => m.DeviceId).Distinct())
                {
                    IReadOnlyList<DownloadEvent> downloadEvents = await _downloadEventRepository.GetByDeviceIdAsync(deviceIdForItems, ct);
                    foreach (DownloadEvent downloadEvent in downloadEvents)
                    {
                        _ = providerEventIdByDownloadEventId.TryAdd(downloadEvent.Id, downloadEvent.ProviderEventId);
                    }
                }

                report.MediaProviderEventLinks = items
                    .Where(m => m.DownloadEventId.HasValue && providerEventIdByDownloadEventId.ContainsKey(m.DownloadEventId.Value))
                    .Select(m => new MediaProviderEventLink(m.Id, providerEventIdByDownloadEventId[m.DownloadEventId!.Value]))
                    .ToList();

                report.MediaItems = items;
                report.IntegrityRecords = integrityRecords;
                report.TotalItemCount = items.Count;
                report.VerifiedItemCount = items.Count(m => m.IntegrityVerified);
                report.FailedVerificationCount = integrityRecords.Count(r => !r.Passed);

                _logger.LogDebug(
                    "Built evidence review report: {TotalCount} items, {VerifiedCount} verified",
                    report.TotalItemCount,
                    report.VerifiedItemCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building evidence review report");
            }

            return report;
        }

        public async Task<ForensicAnalysisReport> BuildForensicAnalysisReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var report = new ForensicAnalysisReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = fromUtc,
                ReportToUtc = toUtc
            };

            try
            {
                IReadOnlyList<MediaItem> items;
                if (deviceId.HasValue)
                {
                    items = await _mediaItemRepository.GetByDeviceAndDateRangeAsync(deviceId.Value, fromUtc, toUtc, ct);
                }
                else
                {
                    IReadOnlyList<MediaItem> allItems = await _mediaItemRepository.ListAsync(ct);
                    items = allItems.Where(m => m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList();
                }

                report.EvidenceItems = items;
                report.Summary = $"Forensic analysis report covering {items.Count} media items from {fromUtc:O} to {toUtc:O}";

                _logger.LogDebug("Built forensic analysis report: {ItemCount} items", items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building forensic analysis report");
            }

            return report;
        }

        public async Task<SignalAnomalyReport> BuildSignalAnomalyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var report = new SignalAnomalyReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = fromUtc,
                ReportToUtc = toUtc
            };

            try
            {
                List<Device> devices;
                if (deviceId.HasValue)
                {
                    Device? device = await _deviceRepository.GetAsync(deviceId.Value, ct);
                    devices = device != null ? [device] : [];
                }
                else
                {
                    devices = (await _deviceRepository.ListAsync(ct)).ToList();
                }

                var anomaliesList = new List<SignalAnomalyReport.AnomalyFindings>();
                var jammingList = new List<SignalAnomalyReport.JammingSummaryEntry>();

                foreach (Device device in devices)
                {
                    var findings = new SignalAnomalyReport.AnomalyFindings
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        Anomalies = []
                    };
                    anomaliesList.Add(findings);

                    JammingStatsSummary? jammingStats = await _jammingRepository.GetStatsAsync(device.Id, ct);
                    if (jammingStats != null)
                    {
                        jammingList.Add(new SignalAnomalyReport.JammingSummaryEntry
                        {
                            DeviceId = device.Id,
                            DeviceName = device.Name,
                            IncidentCount = jammingStats.IncidentCount,
                            TotalJammedDurationMinutes = jammingStats.TotalJammedDurationMinutes,
                            AverageDegradationDb = jammingStats.AverageDegradationDb,
                            MaxDegradationDb = jammingStats.MaxDegradationDb,
                            FirstIncidentUtc = jammingStats.FirstIncidentUtc,
                            LastIncidentUtc = jammingStats.LastIncidentUtc
                        });
                    }
                }

                report.AnomaliesByDevice = anomaliesList;
                report.JammingByDevice = jammingList;
                _logger.LogDebug("Built signal anomaly report for {DeviceCount} devices", devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building signal anomaly report");
            }

            return report;
        }

        public async Task<AccessControlReport> BuildAccessControlReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var report = new AccessControlReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = fromUtc,
                ReportToUtc = toUtc
            };

            try
            {
                IReadOnlyList<ActionLogEntry> allActions = await _actionLogRepository.ListAsync(ct);
                var filteredActions = allActions
                    .Where(a => a.TimestampUtc >= fromUtc && a.TimestampUtc <= toUtc)
                    .Where(a => IsForDevice(a, deviceId))
                    .ToList();

                var accessEvents = filteredActions
                    .Select(a => new AccessControlReport.AccessEvent
                    {
                        AccessedAtUtc = a.TimestampUtc,
                        Actor = a.Actor,
                        Action = a.Action,
                        EntityType = a.EntityType,
                        EntityId = a.EntityId,
                        Details = a.DetailsJson
                    })
                    .ToList();

                report.AccessEvents = accessEvents;
                _logger.LogDebug("Built access control report: {ActionCount} actions", accessEvents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building access control report");
            }

            return report;
        }

        public async Task<ChainOfCustodyReport> BuildChainOfCustodyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var report = new ChainOfCustodyReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = fromUtc,
                ReportToUtc = toUtc
            };

            try
            {
                IReadOnlyList<ActionLogEntry> allActions = await _actionLogRepository.ListAsync(ct);
                var filteredActions = allActions
                    .Where(a => a.TimestampUtc >= fromUtc && a.TimestampUtc <= toUtc)
                    .Where(a => IsForDevice(a, deviceId))
                    .OrderBy(a => a.TimestampUtc)
                    .ToList();

                report.AuditTrail = filteredActions;

                // The hash chain is one chain for the whole log, so the verdict is global whatever device is asked for.
                ChainIntegrityResult chain = await _actionLogRepository.VerifyChainIntegrityAsync(ct);
                report.ChainIntegrityVerified = chain.IsValid;
                report.ChainVerificationStatus = chain.IsValid ? "Valid hash chain" : "Chain integrity check failed";
                report.ChainEntriesChecked = chain.EntriesChecked;
                report.ChainBreak = chain.Break;
                report.ChainFailedEntryId = chain.FailedEntryId;
                report.ChainFailedAtUtc = chain.FailedEntryTimestampUtc;

                _logger.LogDebug(
                    "Built chain of custody report: {ActionCount} actions, chain valid={ChainValid}, break={ChainBreak}",
                    filteredActions.Count,
                    chain.IsValid,
                    chain.Break);

                if (!chain.IsValid)
                {
                    await DispatchChainIntegrityAlertAsync(chain, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building chain of custody report");
            }

            return report;
        }

        /// <summary>
        /// Whether an audit entry belongs to a device's trail. Entries about the device itself match by id; other
        /// entries match when their details name the device. A null device keeps every entry.
        /// </summary>
        private static bool IsForDevice(ActionLogEntry entry, Guid? deviceId)
        {
            return deviceId is not { } id || entry.EntityId == id || (entry.DetailsJson?.Contains(id.ToString("D"), StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>
        /// Raises an Alert-severity notice so a broken audit chain shows in the notices bell.
        /// Failures are isolated: a notification problem must never suppress the report itself.
        /// </summary>
        private async Task DispatchChainIntegrityAlertAsync(ChainIntegrityResult chain, CancellationToken ct)
        {
            if (_notificationDispatcher == null)
            {
                return;
            }

            // Every report build re-checks the chain, so a broken chain would alert on each refresh. Alert at most once per window.
            lock (_chainAlertGate)
            {
                if (DateTime.UtcNow - _lastChainAlertUtc < ChainAlertWindow)
                {
                    return;
                }

                _lastChainAlertUtc = DateTime.UtcNow;
            }

            try
            {
                await _notificationDispatcher.DispatchAsync(
                    new NotificationEvent(
                        EventType: "ChainOfCustodyIntegrityFailed",
                        TimestampUtc: DateTime.UtcNow,
                        OperatorId: null,
                        PairedDeviceId: null,
                        SourceIp: null,
                        Details: $"Chain of custody integrity check failed ({chain.Break}) at entry {chain.FailedEntryId:D} after {chain.EntriesChecked} verified entries.",
                        Audience: NotificationAudience.AdminsOnly,
                        Severity: NoticeSeverity.Alert,
                        ActionPath: "/analyze/chain-of-custody"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch chain of custody integrity alert (non-critical)");
            }
        }

        public async Task WriteReportAsync(object reportDto, string format, CancellationToken ct)
        {
            try
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string reportTypeName = reportDto.GetType().Name;
                string fileName = $"{reportTypeName}_{timestamp}.{format.ToLowerInvariant()}";

                // Determine output directory using storage provider
                string reportsDir = _storageLocationProvider.GetDefaultRoot(StorageCategory.Reports);
                _ = Directory.CreateDirectory(reportsDir);

                string filePath = Path.Combine(reportsDir, fileName);

                switch (format.ToLowerInvariant())
                {
                    case "json":
                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        string jsonContent = JsonSerializer.Serialize(reportDto, jsonOptions);
                        await File.WriteAllTextAsync(filePath, jsonContent, ct);
                        break;

                    case "xml":
                        var xmlSerializer = new System.Xml.Serialization.XmlSerializer(reportDto.GetType());
                        using (var xmlWriter = new StreamWriter(filePath))
                        {
                            xmlSerializer.Serialize(xmlWriter, reportDto);
                        }

                        break;

                    case "csv":
                        // Basic CSV output: serialize as JSON and note the limitation
                        string csvAsJson = JsonSerializer.Serialize(reportDto, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(filePath, $"# CSV output not yet fully implemented; see JSON for full data\n{csvAsJson}", ct);
                        break;

                    default:
                        throw new ArgumentException($"Unsupported report format: {format}");
                }

                _logger.LogInformation("Report written to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing report in format {Format}", format);
                throw;
            }
        }
    }
}
