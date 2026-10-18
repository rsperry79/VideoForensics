using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Extensions.Logging;

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;

namespace VideoForensics.Client.Core.Services
{
    internal class EvidenceExportOrchestrator : IEvidenceExportService
    {
        private readonly ILogger<EvidenceExportOrchestrator> _logger;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IIntegrityVerificationService _integrityVerificationService;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly IExportRecordService _exportRecordService;
        private readonly ICaseRepository? _caseRepository;
        private readonly IReportGenerationService? _reportService;

        private static readonly JsonSerializerOptions ReportJsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        public EvidenceExportOrchestrator(
            ILogger<EvidenceExportOrchestrator> logger,
            IMediaItemRepository mediaItemRepository,
            IIntegrityVerificationService integrityVerificationService,
            IActionLogRepository actionLogRepository,
            IExportRecordService exportRecordService,
            ICaseRepository? caseRepository = null,
            IReportGenerationService? reportService = null)
        {
            _caseRepository = caseRepository;
            _reportService = reportService;
            _logger = logger;
            _mediaItemRepository = mediaItemRepository;
            _integrityVerificationService = integrityVerificationService;
            _actionLogRepository = actionLogRepository;
            _exportRecordService = exportRecordService;
        }

        /// <inheritdoc />
        public async Task<ExportResult> ExportEvidenceAsync(
            Guid caseId,
            string outputDirectory,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct,
            string? stepUpToken = null)
        {
            if (_caseRepository is null || _reportService is null)
            {
                throw new InvalidOperationException("Case export requires the case repository and report generation service.");
            }

            ForensicCase? forensicCase = await _caseRepository.GetAsync(caseId, ct);
            if (forensicCase is null)
            {
                _logger.LogWarning("Case export requested for unknown case {CaseId}", caseId);
                return new ExportResult { Success = false, ErrorMessage = "Case not found." };
            }

            IReadOnlyList<CaseItem> pinned = await _caseRepository.ListItemsAsync(caseId, false, ct);
            List<Guid> mediaItemIds = pinned
                .Where(i => i.Kind == CaseItemKind.Media && i.MediaItemId.HasValue)
                .Select(i => i.MediaItemId!.Value)
                .Distinct()
                .ToList();

            // The case's own material: its scope devices/window plus the devices and recording times of its pinned media.
            // Reports are never built for devices or time ranges the case does not reference.
            var pinnedDeviceIds = new HashSet<Guid>();
            DateTime? earliest = null;
            DateTime? latest = null;
            foreach (Guid mediaItemId in mediaItemIds)
            {
                MediaItem? media = await _mediaItemRepository.GetAsync(mediaItemId, ct);
                if (media is null)
                {
                    continue;
                }

                _ = pinnedDeviceIds.Add(media.DeviceId);
                earliest = earliest is null || media.RecordedAtUtc < earliest ? media.RecordedAtUtc : earliest;
                latest = latest is null || media.RecordedAtUtc > latest ? media.RecordedAtUtc : latest;
            }

            DateTime? fromUtc = forensicCase.ScopeFromUtc ?? earliest;
            DateTime? toUtc = forensicCase.ScopeToUtc
                ?? (latest?.AddSeconds(1) ?? (forensicCase.ScopeFromUtc is not null ? DateTime.UtcNow : null));

            var reportEntries = new List<(string Name, string Json)>();
            if (fromUtc is not null && toUtc is not null)
            {
                IReadOnlyList<Guid> scopeDeviceIds = await _caseRepository.GetDeviceIdsAsync(caseId, ct);
                foreach (Guid deviceId in scopeDeviceIds.Concat(pinnedDeviceIds).Distinct())
                {
                    string suffix = deviceId.ToString();
                    DateTime from = fromUtc.Value;
                    DateTime to = toUtc.Value;
                    reportEntries.Add(($"reports/evidence-review_{suffix}.json", JsonSerializer.Serialize(await _reportService.BuildEvidenceReviewAsync(deviceId, from, to, ct), ReportJsonOptions)));
                    reportEntries.Add(($"reports/forensic-analysis_{suffix}.json", JsonSerializer.Serialize(await _reportService.BuildForensicAnalysisReportAsync(deviceId, from, to, ct), ReportJsonOptions)));
                    reportEntries.Add(($"reports/signal-anomaly_{suffix}.json", JsonSerializer.Serialize(await _reportService.BuildSignalAnomalyReportAsync(deviceId, from, to, ct), ReportJsonOptions)));
                    reportEntries.Add(($"reports/access-control_{suffix}.json", JsonSerializer.Serialize(await _reportService.BuildAccessControlReportAsync(deviceId, from, to, ct), ReportJsonOptions)));
                    reportEntries.Add(($"reports/chain-of-custody_{suffix}.json", JsonSerializer.Serialize(await _reportService.BuildChainOfCustodyReportAsync(deviceId, from, to, ct), ReportJsonOptions)));
                }
            }
            else
            {
                _logger.LogInformation("Case {CaseId} defines no time window and has no pinned media; exporting no reports", caseId);
            }

            return await ExportCoreAsync(mediaItemIds, outputDirectory, forensicCase.CaseNumber, recipientDescription, passphrase, reportEntries, ct);
        }

        /// <summary>Exports an explicit set of media items, not tied to a case. Not part of <see cref="IEvidenceExportService"/>.</summary>
        public Task<ExportResult> ExportEvidenceAsync(
            IReadOnlyList<Guid> mediaItemIds,
            string outputDirectory,
            string? caseReference,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct,
            string? stepUpToken = null) =>
            ExportCoreAsync(mediaItemIds, outputDirectory, caseReference, recipientDescription, passphrase, [], ct);

        private async Task<ExportResult> ExportCoreAsync(
            IReadOnlyList<Guid> mediaItemIds,
            string outputDirectory,
            string? caseReference,
            string? recipientDescription,
            string? passphrase,
            IReadOnlyList<(string Name, string Json)> extraEntries,
            CancellationToken ct)
        {
            // The step-up token is checked by the host before this runs (the WebApp's circuit guard). The local
            // export does not re-check it, so it must only be reached through that guard.
            string resolvedOutputDirectory = ResolveSafeOutputDirectory(outputDirectory);
            var result = new ExportResult();

            try
            {
                _logger.LogInformation(
                    "Starting evidence export for {ItemCount} item(s). Case: {CaseRef}, Recipient: {Recipient}, Encrypted: {IsEncrypted}",
                    mediaItemIds.Count,
                    SanitizeForLog(caseReference ?? "[no case reference]"),
                    SanitizeForLog(recipientDescription ?? "[no recipient]"),
                    !string.IsNullOrEmpty(passphrase));

                // Ensure output directory exists
                _ = Directory.CreateDirectory(resolvedOutputDirectory);

                // Step 1: Fetch all media items and verify integrity
                var itemsToExport = new List<(MediaItem Item, string Sha256AtExport)>();
                var excludedItems = new List<Guid>();

                foreach (Guid mediaItemId in mediaItemIds)
                {
                    try
                    {
                        MediaItem? mediaItem = await _mediaItemRepository.GetAsync(mediaItemId, ct);
                        if (mediaItem == null)
                        {
                            _logger.LogWarning("Media item {MediaItemId} not found, skipping", mediaItemId);
                            excludedItems.Add(mediaItemId);
                            continue;
                        }

                        // Verify integrity; exclude items that fail verification
                        bool verificationPassed = await _integrityVerificationService.VerifyAsync(mediaItemId, ct);
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

                if (itemsToExport.Count == 0 && extraEntries.Count == 0)
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
                        x.Item.FileName,
                        Sha256Hash = x.Sha256AtExport,
                        x.Item.RecordedAtUtc,
                        x.Item.DownloadedAtUtc,
                        IntegrityStatus = "Verified"
                    }).ToList()
                };

                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

                // Step 3: Build chain_of_custody.json (per-item action log history)
                var chainOfCustodyItems = new List<object>();
                foreach ((MediaItem? item, _) in itemsToExport)
                {
                    try
                    {
                        IReadOnlyList<ActionLogEntry> history = await _actionLogRepository.GetHistoryForEntityAsync(nameof(MediaItem), item.Id, ct);
                        chainOfCustodyItems.Add(new
                        {
                            MediaItemId = item.Id,
                            item.FileName,
                            ActionHistory = history.Select(entry => new
                            {
                                entry.TimestampUtc,
                                entry.Actor,
                                ActorType = entry.ActorType.ToString(),
                                entry.Action,
                                Details = entry.DetailsJson
                            }).ToList()
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to retrieve action log for media item {MediaItemId}, skipping history", item.Id);
                    }
                }

                string chainOfCustodyJson = JsonSerializer.Serialize(chainOfCustodyItems, new JsonSerializerOptions { WriteIndented = true });

                // Step 4: Create ZIP archive
                string archiveFileName = $"Evidence_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                string archivePath = EnsureWithinRoot(resolvedOutputDirectory, Path.Combine(resolvedOutputDirectory, archiveFileName));

                using (var zipStream = new ZipOutputStream(File.Create(archivePath)))
                {
                    if (!string.IsNullOrEmpty(passphrase))
                    {
                        zipStream.Password = passphrase;
                    }

                    // Add each media file
                    foreach ((MediaItem? item, _) in itemsToExport)
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

                            using (FileStream fileStream = File.OpenRead(item.FilePath))
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

                    foreach ((string name, string json) in extraEntries)
                    {
                        zipStream.PutNextEntry(new ZipEntry(name));
                        using (var writer = new StreamWriter(zipStream, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            await writer.WriteAsync(json);
                        }

                        zipStream.CloseEntry();
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
                string archiveHash = await ComputeFileHashAsync(archivePath, ct);
                var exportedItems = itemsToExport.Select(x => (x.Item.Id, x.Sha256AtExport)).ToList();

                _ = await _exportRecordService.RecordExportAsync(
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
                    SanitizeForLog(archivePath),
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
            using var hashAlgorithm = SHA256.Create();
            using FileStream fileStream = File.OpenRead(filePath);
            byte[] hash = await hashAlgorithm.ComputeHashAsync(fileStream, ct);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string ResolveSafeOutputDirectory(string requestedOutputDirectory)
        {
            if (string.IsNullOrWhiteSpace(requestedOutputDirectory))
            {
                throw new InvalidOperationException("Output directory must be provided.");
            }

            if (Path.IsPathRooted(requestedOutputDirectory))
            {
                throw new InvalidOperationException("Absolute output paths are not allowed.");
            }

            string normalizedRelative = Path.GetFullPath(Path.Combine(".", requestedOutputDirectory));
            string currentDir = Path.GetFullPath(".");
            if (!normalizedRelative.StartsWith(currentDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedRelative, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Output directory contains invalid parent directory traversal.");
            }

            string baseExportRoot = Path.Combine(AppContext.BaseDirectory, "exports");
            _ = Directory.CreateDirectory(baseExportRoot);
            string combined = Path.Combine(baseExportRoot, requestedOutputDirectory);
            string safeOutputDirectory = EnsureWithinRoot(baseExportRoot, combined);
            _ = Directory.CreateDirectory(safeOutputDirectory);
            return safeOutputDirectory;
        }

        /// <summary>
        /// Validates that a resolved path stays within the intended root directory.
        /// Prevents path traversal attacks by ensuring the resolved path does not escape the root.
        /// </summary>
        /// <param name="root">The root directory that the candidate path must stay within.</param>
        /// <param name="candidatePath">The path to validate (may contain relative components or symlinks).</param>
        /// <returns>The canonicalized (full) path, if it stays within root.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the resolved path escapes the root directory.</exception>
        private static string EnsureWithinRoot(string root, string candidatePath)
        {
            string fullRoot = Path.GetFullPath(root);
            string fullCandidate = Path.GetFullPath(candidatePath);
            if (!fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Resolved path '{fullCandidate}' escapes the intended output directory '{fullRoot}'.");
            }

            return fullCandidate;
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');
    }
}
