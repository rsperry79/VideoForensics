using System.Security.Cryptography;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Core
{
    internal class EvidenceExportOrchestrator : IEvidenceExportService
    {
        private readonly ILogger<EvidenceExportOrchestrator> _logger;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IIntegrityVerificationService _integrityVerificationService;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly IExportRecordService _exportRecordService;

        public EvidenceExportOrchestrator(
            ILogger<EvidenceExportOrchestrator> logger,
            IMediaItemRepository mediaItemRepository,
            IIntegrityVerificationService integrityVerificationService,
            IActionLogRepository actionLogRepository,
            IExportRecordService exportRecordService)
        {
            _logger = logger;
            _mediaItemRepository = mediaItemRepository;
            _integrityVerificationService = integrityVerificationService;
            _actionLogRepository = actionLogRepository;
            _exportRecordService = exportRecordService;
        }

        public async Task<ExportResult> ExportEvidenceAsync(
            IReadOnlyList<Guid> mediaItemIds,
            string outputDirectory,
            string? caseReference,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct)
        {
            var result = new ExportResult();

            try
            {
                _logger.LogInformation(
                    "Starting evidence export for {ItemCount} item(s). Case: {CaseRef}, Recipient: {Recipient}, Encrypted: {IsEncrypted}",
                    mediaItemIds.Count,
                    caseReference ?? "[no case reference]",
                    recipientDescription ?? "[no recipient]",
                    !string.IsNullOrEmpty(passphrase));

                // Ensure output directory exists
                Directory.CreateDirectory(outputDirectory);

                // Step 1: Fetch all media items and verify integrity
                var itemsToExport = new List<(MediaItem Item, string Sha256AtExport)>();
                var excludedItems = new List<Guid>();

                foreach (var mediaItemId in mediaItemIds)
                {
                    try
                    {
                        var mediaItem = await _mediaItemRepository.GetAsync(mediaItemId, ct);
                        if (mediaItem == null)
                        {
                            _logger.LogWarning("Media item {MediaItemId} not found, skipping", mediaItemId);
                            excludedItems.Add(mediaItemId);
                            continue;
                        }

                        // Verify integrity; exclude items that fail verification
                        var verificationPassed = await _integrityVerificationService.VerifyAsync(mediaItemId, ct);
                        if (!verificationPassed)
                        {
                            _logger.LogWarning(
                                "Media item {MediaItemId} failed integrity verification. Excluding from export.",
                                mediaItemId);
                            excludedItems.Add(mediaItemId);
                            continue;
                        }

                        // Capture the hash at export time
                        itemsToExport.Add((mediaItem, mediaItem.Sha256Hash));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing media item {MediaItemId} for export, skipping", mediaItemId);
                        excludedItems.Add(mediaItemId);
                    }
                }

                if (itemsToExport.Count == 0)
                {
                    result.ErrorMessage = "No media items were suitable for export (all failed integrity verification or not found)";
                    _logger.LogError(result.ErrorMessage);
                    return result;
                }

                // Step 2: Build manifest.json
                var manifest = new
                {
                    ExportedAtUtc = DateTime.UtcNow,
                    ExportedByUserName = Environment.UserName,
                    CaseReference = caseReference,
                    RecipientDescription = recipientDescription,
                    ItemCount = itemsToExport.Count,
                    Items = itemsToExport.Select(x => new
                    {
                        FileName = x.Item.FileName,
                        Sha256Hash = x.Sha256AtExport,
                        RecordedAtUtc = x.Item.RecordedAtUtc,
                        DownloadedAtUtc = x.Item.DownloadedAtUtc,
                        IntegrityStatus = "Verified"
                    }).ToList()
                };

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

                // Step 3: Build chain_of_custody.json (per-item action log history)
                var chainOfCustodyItems = new List<object>();
                foreach (var (item, _) in itemsToExport)
                {
                    try
                    {
                        var history = await _actionLogRepository.GetHistoryForEntityAsync(nameof(MediaItem), item.Id, ct);
                        chainOfCustodyItems.Add(new
                        {
                            MediaItemId = item.Id,
                            FileName = item.FileName,
                            ActionHistory = history.Select(entry => new
                            {
                                TimestampUtc = entry.TimestampUtc,
                                Actor = entry.Actor,
                                ActorType = entry.ActorType.ToString(),
                                Action = entry.Action,
                                Details = entry.DetailsJson
                            }).ToList()
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve action log for media item {MediaItemId}, skipping history", item.Id);
                    }
                }

                var chainOfCustodyJson = JsonSerializer.Serialize(chainOfCustodyItems, new JsonSerializerOptions { WriteIndented = true });

                // Step 4: Create ZIP archive
                var archiveFileName = $"Evidence_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                var archivePath = Path.Combine(outputDirectory, archiveFileName);

                using (var zipStream = new ZipOutputStream(File.Create(archivePath)))
                {
                    if (!string.IsNullOrEmpty(passphrase))
                    {
                        zipStream.Password = passphrase;
                    }

                    // Add each media file
                    foreach (var (item, _) in itemsToExport)
                    {
                        try
                        {
                            if (!File.Exists(item.FilePath))
                            {
                                _logger.LogWarning("Media file not found at {FilePath}, skipping", item.FilePath);
                                continue;
                            }

                            var entry = new ZipEntry(item.FileName);
                            zipStream.PutNextEntry(entry);

                            using (var fileStream = File.OpenRead(item.FilePath))
                            {
                                await fileStream.CopyToAsync(zipStream, ct);
                            }

                            zipStream.CloseEntry();
                            _logger.LogInformation("Added {FileName} to archive", item.FileName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to add media file {FileName} to archive", item.FileName);
                        }
                    }

                    // Add manifest.json
                    {
                        var entry = new ZipEntry("manifest.json");
                        zipStream.PutNextEntry(entry);
                        using (var writer = new StreamWriter(zipStream, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            await writer.WriteAsync(manifestJson);
                        }
                        zipStream.CloseEntry();
                    }

                    // Add chain_of_custody.json
                    {
                        var entry = new ZipEntry("chain_of_custody.json");
                        zipStream.PutNextEntry(entry);
                        using (var writer = new StreamWriter(zipStream, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            await writer.WriteAsync(chainOfCustodyJson);
                        }
                        zipStream.CloseEntry();
                    }
                }

                // Step 5: Compute archive hash and record the export
                var archiveHash = await ComputeFileHashAsync(archivePath, ct);
                var exportedItems = itemsToExport.Select(x => (x.Item.Id, x.Sha256AtExport)).ToList();

                await _exportRecordService.RecordExportAsync(
                    Environment.UserName,
                    caseReference,
                    recipientDescription,
                    archiveFileName,
                    archiveHash,
                    wasEncrypted: !string.IsNullOrEmpty(passphrase),
                    exportedItems,
                    ct);

                result.Success = true;
                result.ArchivePath = archivePath;
                result.ArchiveSha256Hash = archiveHash;
                result.ItemsIncluded = itemsToExport.Count;
                result.ItemsExcludedForFailedIntegrity = excludedItems;

                _logger.LogInformation(
                    "Export completed successfully: {ArchivePath}, {ItemCount} items, hash={Hash}, encrypted={IsEncrypted}",
                    archivePath,
                    itemsToExport.Count,
                    archiveHash,
                    !string.IsNullOrEmpty(passphrase));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evidence export failed");
                result.Success = false;
                result.ErrorMessage = $"Export failed: {ex.Message}";
                return result;
            }
        }

        private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
        {
            using (var hashAlgorithm = SHA256.Create())
            using (var fileStream = File.OpenRead(filePath))
            {
                var hash = await hashAlgorithm.ComputeHashAsync(fileStream, ct);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
