using System.Text.Json;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

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
        private readonly ILogger<ReportGenerationService> _logger;

        public ReportGenerationService(
            IMediaItemRepository mediaItemRepository,
            IDeviceRepository deviceRepository,
            IDownloadEventRepository downloadEventRepository,
            IActionLogRepository actionLogRepository,
            IEventRepository eventRepository,
            ILogger<ReportGenerationService> logger)
        {
            _mediaItemRepository = mediaItemRepository;
            _deviceRepository = deviceRepository;
            _downloadEventRepository = downloadEventRepository;
            _actionLogRepository = actionLogRepository;
            _eventRepository = eventRepository;
            _logger = logger;
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
                    var allItems = await _mediaItemRepository.ListAsync(ct);
                    items = allItems.Where(m => m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList();
                }

                report.MediaItems = items;
                report.TotalItemCount = items.Count;
                report.VerifiedItemCount = items.Count(m => m.IntegrityVerified);
                report.FailedVerificationCount = 0; // Would need IntegrityRecord lookup to populate accurately

                _logger.LogInformation(
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
                    var allItems = await _mediaItemRepository.ListAsync(ct);
                    items = allItems.Where(m => m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList();
                }

                report.EvidenceItems = items;
                report.Summary = $"Forensic analysis report covering {items.Count} media items from {fromUtc:O} to {toUtc:O}";

                _logger.LogInformation("Built forensic analysis report: {ItemCount} items", items.Count);
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
                    var device = await _deviceRepository.GetAsync(deviceId.Value, ct);
                    devices = device != null ? new List<Device> { device } : new List<Device>();
                }
                else
                {
                    devices = (await _deviceRepository.ListAsync(ct)).ToList();
                }

                var anomaliesList = new List<SignalAnomalyReport.AnomalyFindings>();

                foreach (var device in devices)
                {
                    var findings = new SignalAnomalyReport.AnomalyFindings
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        Anomalies = new List<SignalAnomalyReport.SignalAnomaly>()
                    };
                    anomaliesList.Add(findings);
                }

                report.AnomaliesByDevice = anomaliesList;
                _logger.LogInformation("Built signal anomaly report for {DeviceCount} devices", devices.Count);
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
                var allActions = await _actionLogRepository.ListAsync(ct);
                var filteredActions = allActions
                    .Where(a => a.TimestampUtc >= fromUtc && a.TimestampUtc <= toUtc)
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
                _logger.LogInformation("Built access control report: {ActionCount} actions", accessEvents.Count);
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
                var allActions = await _actionLogRepository.ListAsync(ct);
                var filteredActions = allActions
                    .Where(a => a.TimestampUtc >= fromUtc && a.TimestampUtc <= toUtc)
                    .OrderBy(a => a.TimestampUtc)
                    .ToList();

                report.AuditTrail = filteredActions;

                var chainIsValid = await _actionLogRepository.VerifyChainIntegrityAsync(ct);
                report.ChainIntegrityVerified = chainIsValid;
                report.ChainVerificationStatus = chainIsValid ? "Valid hash chain" : "Chain integrity check failed";

                _logger.LogInformation(
                    "Built chain of custody report: {ActionCount} actions, chain valid={ChainValid}",
                    filteredActions.Count,
                    chainIsValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building chain of custody report");
            }

            return report;
        }

        public async Task WriteReportAsync(object reportDto, string format, CancellationToken ct)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var reportTypeName = reportDto.GetType().Name;
                var fileName = $"{reportTypeName}_{timestamp}.{format.ToLowerInvariant()}";

                // Determine output directory (using a default reports directory)
                var reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics",
                    "Reports");
                Directory.CreateDirectory(reportsDir);

                var filePath = Path.Combine(reportsDir, fileName);

                switch (format.ToLowerInvariant())
                {
                    case "json":
                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        var jsonContent = JsonSerializer.Serialize(reportDto, jsonOptions);
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
                        var csvAsJson = JsonSerializer.Serialize(reportDto, new JsonSerializerOptions { WriteIndented = true });
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
