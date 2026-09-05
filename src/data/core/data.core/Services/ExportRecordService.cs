using Microsoft.Extensions.Logging;

using System.Text.Json;

using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for recording evidence export operations.</summary>
    internal class ExportRecordService : IExportRecordService
    {
        private readonly IExportRecordRepository _exportRecordRepository;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActionLogger _actionLogger;
        private readonly ILogger<ExportRecordService> _logger;

        public ExportRecordService(
            IExportRecordRepository exportRecordRepository,
            IMediaItemRepository mediaItemRepository,
            IUnitOfWork unitOfWork,
            IActionLogger actionLogger,
            ILogger<ExportRecordService> logger)
        {
            _exportRecordRepository = exportRecordRepository;
            _mediaItemRepository = mediaItemRepository;
            _unitOfWork = unitOfWork;
            _actionLogger = actionLogger;
            _logger = logger;
        }

        public async Task<ExportRecord> RecordExportAsync(
            string exportedByUserName,
            string? caseReference,
            string? recipientDescription,
            string archiveFileName,
            string archiveSha256Hash,
            bool wasEncrypted,
            IReadOnlyList<(Guid MediaItemId, string HashAtExport)> items,
            CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    string appVersion = GetAppVersion();

                    var exportRecord = new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedAtUtc = DateTime.UtcNow,
                        ExportedByUserName = exportedByUserName,
                        CaseReference = caseReference,
                        RecipientDescription = recipientDescription,
                        ArchiveFileName = archiveFileName,
                        ArchiveSha256Hash = archiveSha256Hash,
                        WasEncrypted = wasEncrypted,
                        ItemCount = items.Count,
                        AppVersion = appVersion
                    };

                    var exportItems = items.Select(item => new ExportRecordItem
                    {
                        Id = Guid.NewGuid(),
                        ExportRecordId = exportRecord.Id,
                        MediaItemId = item.MediaItemId,
                        MediaItemSha256HashAtExport = item.HashAtExport
                    }).ToList();

                    _ = await context.ExportRecords.AppendAsync(exportRecord, exportItems, ct);

                    // Log the export event
                    var details = new
                    {
                        CaseReference = caseReference,
                        RecipientDescription = recipientDescription,
                        ItemCount = items.Count,
                        WasEncrypted = wasEncrypted
                    };

                    _ = await context.ActionLog.AppendAsync(
                        exportedByUserName,
                        ActorType.Human,
                        "EvidenceExported",
                        nameof(ExportRecord),
                        exportRecord.Id,
                        JsonSerializer.Serialize(details),
                        ct);

                    _logger.LogInformation(
                        "Export recorded: {ItemCount} items, archive={FileName}, encrypted={WasEncrypted}, case={CaseReference}",
                        items.Count,
                        archiveFileName,
                        wasEncrypted,
                        caseReference ?? "[no case reference]");

                    return exportRecord;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording export: {ArchiveFileName}", archiveFileName);
                throw;
            }
        }

        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct)
        {
            try
            {
                return await _exportRecordRepository.GetHistoryForMediaItemAsync(mediaItemId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving export history for media item {MediaItemId}", mediaItemId);
                throw;
            }
        }

        public async Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                return await _exportRecordRepository.GetHistoryForDeviceAsync(deviceId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving export history for device {DeviceId}", deviceId);
                throw;
            }
        }

        private string GetAppVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                string version = assembly?.GetName().Version?.ToString() ?? "Unknown";
                var infoVersion = assembly?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;

                return infoVersion?.InformationalVersion ?? version;
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
