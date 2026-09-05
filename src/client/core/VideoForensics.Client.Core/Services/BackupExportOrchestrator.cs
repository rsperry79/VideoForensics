using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Extensions.Logging;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Client.Core.Services
{
    internal class BackupExportOrchestrator : IBackupExportService
    {
        private readonly ILogger<BackupExportOrchestrator> _logger;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IDownloadEventRepository _downloadEventRepository;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IForensicsConfiguration _forensicsConfiguration;
        private readonly IMediaMetadataTagger _metadataTagger;

        public BackupExportOrchestrator(
            ILogger<BackupExportOrchestrator> logger,
            IProviderAccountRepository providerAccountRepository,
            ILocationRepository locationRepository,
            IDeviceRepository deviceRepository,
            IEventRepository eventRepository,
            IDownloadEventRepository downloadEventRepository,
            IMediaItemRepository mediaItemRepository,
            IForensicsConfiguration forensicsConfiguration,
            IMediaMetadataTagger metadataTagger)
        {
            _logger = logger;
            _providerAccountRepository = providerAccountRepository;
            _locationRepository = locationRepository;
            _deviceRepository = deviceRepository;
            _eventRepository = eventRepository;
            _downloadEventRepository = downloadEventRepository;
            _mediaItemRepository = mediaItemRepository;
            _forensicsConfiguration = forensicsConfiguration;
            _metadataTagger = metadataTagger;
        }

        public async Task<PrepareExportResult> PrepareForExportAsync(CancellationToken ct)
        {
            var result = new PrepareExportResult();

            IReadOnlyList<Event> events = await _eventRepository.ListAsync(ct);
            foreach (Event evt in events)
            {
                if (!evt.DownloadedAtUtc.HasValue)
                {
                    continue;
                }

                DownloadEvent? downloadEvent = await _downloadEventRepository.GetByProviderEventIdAsync(evt.DeviceId, evt.ProviderEventId, ct);
                if (downloadEvent == null)
                {
                    continue;
                }

                IReadOnlyList<MediaItem> mediaItems = await _mediaItemRepository.GetByDownloadEventIdAsync(downloadEvent.Id, ct);
                foreach (MediaItem mediaItem in mediaItems)
                {
                    if (!File.Exists(mediaItem.FilePath))
                    {
                        result.MissingMedia++;
                        result.Details.Add($"Missing media file for event {evt.Id}: {mediaItem.FilePath}");
                        continue;
                    }

                    var sidecarPath = Path.ChangeExtension(mediaItem.FilePath, ".json");
                    Guid? sidecarEventId = TryReadSidecarEventId(sidecarPath);
                    if (sidecarEventId == evt.Id)
                    {
                        result.Validated++;
                        continue;
                    }

                    try
                    {
                        await BackfillSidecarAsync(sidecarPath, evt.Id, ct);
                        _ = await _metadataTagger.TagEventIdAsync(mediaItem.FilePath, evt.Id, ct);
                        result.Backfilled++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to backfill sidecar for event {EventId}", evt.Id);
                        result.MissingSidecarUnrecoverable++;
                        result.Details.Add($"Could not reconcile sidecar for event {evt.Id}: {mediaItem.FilePath}");
                    }
                }
            }

            return result;
        }

        public async Task<BackupExportResult> ExportBackupAsync(string outputDirectory, CancellationToken ct)
        {
            var result = new BackupExportResult();

            try
            {
                _ = Directory.CreateDirectory(outputDirectory);

                IReadOnlyList<ProviderAccount> accounts = await _providerAccountRepository.ListAsync(ct);
                IReadOnlyList<VideoForensics.Data.Common.Entities.Location> locations = await _locationRepository.ListAsync(ct);
                IReadOnlyList<VideoForensics.Data.Common.Entities.Device> devices = await _deviceRepository.ListAsync(ct);
                IReadOnlyList<Event> events = await _eventRepository.ListAsync(ct);
                IReadOnlyList<DownloadEvent> downloadEvents = await _downloadEventRepository.ListAsync(ct);
                IReadOnlyList<MediaItem> mediaItems = await _mediaItemRepository.ListAsync(ct);

                var downloadLocationRoot = _forensicsConfiguration.DownloadLocation ?? string.Empty;
                List<ExportedMediaItem> exportedMediaItems = mediaItems
                    .Select(m => ExportedMediaItem.FromMediaItem(m, downloadLocationRoot))
                    .ToList();

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var accountsJson = JsonSerializer.Serialize(accounts, jsonOptions);
                var locationsJson = JsonSerializer.Serialize(locations, jsonOptions);
                var devicesJson = JsonSerializer.Serialize(devices, jsonOptions);
                var eventsJson = JsonSerializer.Serialize(events, jsonOptions);
                var downloadEventsJson = JsonSerializer.Serialize(downloadEvents, jsonOptions);
                var mediaItemsJson = JsonSerializer.Serialize(exportedMediaItems, jsonOptions);

                var manifest = new
                {
                    ExportedAtUtc = DateTime.UtcNow,
                    AppVersion = typeof(BackupExportOrchestrator).Assembly.GetName().Version?.ToString() ?? "unknown",
                    DownloadLocationRoot = downloadLocationRoot,
                    Counts = new
                    {
                        ProviderAccounts = accounts.Count,
                        Locations = locations.Count,
                        Devices = devices.Count,
                        Events = events.Count,
                        DownloadEvents = downloadEvents.Count,
                        MediaItems = mediaItems.Count
                    }
                };
                var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);

                var archiveFileName = $"VideoForensics_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                var archivePath = Path.Combine(outputDirectory, archiveFileName);

                using (var zipStream = new ZipOutputStream(File.Create(archivePath)))
                {
                    await WriteJsonEntryAsync(zipStream, "manifest.json", manifestJson, ct);
                    await WriteJsonEntryAsync(zipStream, "accounts.json", accountsJson, ct);
                    await WriteJsonEntryAsync(zipStream, "locations.json", locationsJson, ct);
                    await WriteJsonEntryAsync(zipStream, "devices.json", devicesJson, ct);
                    await WriteJsonEntryAsync(zipStream, "events.json", eventsJson, ct);
                    await WriteJsonEntryAsync(zipStream, "download_events.json", downloadEventsJson, ct);
                    await WriteJsonEntryAsync(zipStream, "media_items.json", mediaItemsJson, ct);
                }

                result.ArchiveSha256Hash = await ComputeFileHashAsync(archivePath, ct);
                result.ArchivePath = archivePath;
                result.ProviderAccountCount = accounts.Count;
                result.LocationCount = locations.Count;
                result.DeviceCount = devices.Count;
                result.EventCount = events.Count;
                result.DownloadEventCount = downloadEvents.Count;
                result.MediaItemCount = mediaItems.Count;
                result.Success = true;

                _logger.LogInformation("Backup export completed: {ArchivePath}", archivePath);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup export failed");
                result.Success = false;
                result.ErrorMessage = $"Export failed: {ex.Message}";
                return result;
            }
        }

        private static Guid? TryReadSidecarEventId(string sidecarPath)
        {
            if (!File.Exists(sidecarPath))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(sidecarPath));
                if (doc.RootElement.TryGetProperty("EventDbId", out JsonElement prop) &&
                    Guid.TryParse(prop.GetString(), out Guid id))
                {
                    return id;
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private static async Task BackfillSidecarAsync(string sidecarPath, Guid eventId, CancellationToken ct)
        {
            Dictionary<string, object?> data = new();
            if (File.Exists(sidecarPath))
            {
                try
                {
                    Dictionary<string, object?>? existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        await File.ReadAllTextAsync(sidecarPath, ct));
                    if (existing != null)
                    {
                        data = existing;
                    }
                }
                catch (JsonException)
                {
                }
            }

            data["EventDbId"] = eventId;
            data["BackfilledAtUtc"] = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(sidecarPath, json, ct);
        }

        private static async Task WriteJsonEntryAsync(ZipOutputStream zipStream, string entryName, string json, CancellationToken ct)
        {
            var entry = new ZipEntry(entryName);
            zipStream.PutNextEntry(entry);
            using (var writer = new StreamWriter(zipStream, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteAsync(json.AsMemory(), ct);
            }

            zipStream.CloseEntry();
        }

        private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
        {
            using var hashAlgorithm = SHA256.Create();
            using FileStream fileStream = File.OpenRead(filePath);
            var hash = await hashAlgorithm.ComputeHashAsync(fileStream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
