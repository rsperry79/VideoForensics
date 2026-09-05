using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Extensions.Logging;

using System.Security.Cryptography;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Core.Services
{
    internal class BackupImportOrchestrator : IBackupImportService
    {
        private readonly ILogger<BackupImportOrchestrator> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public BackupImportOrchestrator(ILogger<BackupImportOrchestrator> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public async Task<BackupImportResult> ImportBackupAsync(string backupZipPath, string mediaRootPath, CancellationToken ct)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "VideoForensicsImport_" + Guid.NewGuid());

            try
            {
                _ = Directory.CreateDirectory(tempDir);

                using (var zipFile = new ZipFile(backupZipPath))
                {
                    foreach (ZipEntry entry in zipFile)
                    {
                        if (!entry.IsFile)
                        {
                            continue;
                        }

                        var outPath = Path.Combine(tempDir, entry.Name);
                        using System.IO.Stream zipStream = zipFile.GetInputStream(entry);
                        using FileStream outStream = File.Create(outPath);
                        await zipStream.CopyToAsync(outStream, ct);
                    }
                }

                var accounts = await ReadJsonAsync<List<ProviderAccount>>(Path.Combine(tempDir, "accounts.json"), ct) ?? new();
                var locations = await ReadJsonAsync<List<Location>>(Path.Combine(tempDir, "locations.json"), ct) ?? new();
                var devices = await ReadJsonAsync<List<Device>>(Path.Combine(tempDir, "devices.json"), ct) ?? new();
                var events = await ReadJsonAsync<List<Event>>(Path.Combine(tempDir, "events.json"), ct) ?? new();
                var downloadEvents = await ReadJsonAsync<List<DownloadEvent>>(Path.Combine(tempDir, "download_events.json"), ct) ?? new();
                var mediaItems = await ReadJsonAsync<List<ExportedMediaItem>>(Path.Combine(tempDir, "media_items.json"), ct) ?? new();

                BackupImportResult result = await _unitOfWork.ExecuteAsync(async ctx =>
                {
                    var importResult = new BackupImportResult();

                    var importedAccountIds = new HashSet<Guid>();
                    foreach (ProviderAccount account in accounts)
                    {
                        if (await ctx.ProviderAccounts.GetAsync(account.Id, ct) != null)
                        {
                            importResult.ProviderAccounts.SkippedExisting++;
                            importedAccountIds.Add(account.Id);
                            continue;
                        }

                        if (await ctx.Users.GetAsync(account.UserId, ct) == null)
                        {
                            importResult.ProviderAccounts.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned ProviderAccount {account.Id}: user {account.UserId} not found");
                            continue;
                        }

                        await ctx.ProviderAccounts.AddAsync(account, ct);
                        importResult.ProviderAccounts.Inserted++;
                        importedAccountIds.Add(account.Id);
                    }

                    var importedLocationIds = new HashSet<Guid>();
                    foreach (Location location in locations)
                    {
                        if (await ctx.Locations.GetAsync(location.Id, ct) != null)
                        {
                            importResult.Locations.SkippedExisting++;
                            importedLocationIds.Add(location.Id);
                            continue;
                        }

                        var accountOk = importedAccountIds.Contains(location.ProviderAccountId) ||
                            await ctx.ProviderAccounts.GetAsync(location.ProviderAccountId, ct) != null;
                        if (!accountOk)
                        {
                            importResult.Locations.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned Location {location.Id}: account {location.ProviderAccountId} not found");
                            continue;
                        }

                        await ctx.Locations.AddAsync(location, ct);
                        importResult.Locations.Inserted++;
                        importedLocationIds.Add(location.Id);
                    }

                    var importedDeviceIds = new HashSet<Guid>();
                    foreach (Device device in devices)
                    {
                        if (await ctx.Devices.GetAsync(device.Id, ct) != null)
                        {
                            importResult.Devices.SkippedExisting++;
                            importedDeviceIds.Add(device.Id);
                            continue;
                        }

                        var locationOk = importedLocationIds.Contains(device.LocationId) ||
                            await ctx.Locations.GetAsync(device.LocationId, ct) != null;
                        if (!locationOk)
                        {
                            importResult.Devices.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned Device {device.Id}: location {device.LocationId} not found");
                            continue;
                        }

                        await ctx.Devices.AddAsync(device, ct);
                        importResult.Devices.Inserted++;
                        importedDeviceIds.Add(device.Id);
                    }

                    var importedEventIds = new HashSet<Guid>();
                    foreach (Event evt in events)
                    {
                        if (await ctx.Events.GetAsync(evt.Id, ct) != null)
                        {
                            importResult.Events.SkippedExisting++;
                            importedEventIds.Add(evt.Id);
                            continue;
                        }

                        var deviceOk = importedDeviceIds.Contains(evt.DeviceId) ||
                            await ctx.Devices.GetAsync(evt.DeviceId, ct) != null;
                        if (!deviceOk)
                        {
                            importResult.Events.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned Event {evt.Id}: device {evt.DeviceId} not found");
                            continue;
                        }

                        _ = await ctx.Events.UpsertAsync(evt, ct);
                        importResult.Events.Inserted++;
                        importedEventIds.Add(evt.Id);
                    }

                    var importedDownloadEventIds = new HashSet<Guid>();
                    foreach (DownloadEvent de in downloadEvents)
                    {
                        if (await ctx.DownloadEvents.GetAsync(de.Id, ct) != null)
                        {
                            importResult.DownloadEvents.SkippedExisting++;
                            importedDownloadEventIds.Add(de.Id);
                            continue;
                        }

                        var deviceOk = importedDeviceIds.Contains(de.DeviceId) ||
                            await ctx.Devices.GetAsync(de.DeviceId, ct) != null;
                        if (!deviceOk)
                        {
                            importResult.DownloadEvents.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned DownloadEvent {de.Id}: device {de.DeviceId} not found");
                            continue;
                        }

                        await ctx.DownloadEvents.AddAsync(de, ct);
                        importResult.DownloadEvents.Inserted++;
                        importedDownloadEventIds.Add(de.Id);
                    }

                    foreach (ExportedMediaItem exported in mediaItems)
                    {
                        MediaItem mediaItem = exported.ToMediaItem(mediaRootPath);

                        if (await ctx.MediaItems.GetAsync(mediaItem.Id, ct) != null)
                        {
                            importResult.MediaItems.SkippedExisting++;
                            continue;
                        }

                        var deviceOk = importedDeviceIds.Contains(mediaItem.DeviceId) ||
                            await ctx.Devices.GetAsync(mediaItem.DeviceId, ct) != null;
                        var downloadEventOk = mediaItem.DownloadEventId == null ||
                            importedDownloadEventIds.Contains(mediaItem.DownloadEventId.Value) ||
                            await ctx.DownloadEvents.GetAsync(mediaItem.DownloadEventId.Value, ct) != null;

                        if (!deviceOk || !downloadEventOk)
                        {
                            importResult.MediaItems.SkippedOrphaned++;
                            importResult.Details.Add($"Skipped orphaned MediaItem {mediaItem.Id}");
                            continue;
                        }

                        if (File.Exists(mediaItem.FilePath))
                        {
                            var hashBytes = await SHA256.HashDataAsync(File.OpenRead(mediaItem.FilePath), ct);
                            var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                            mediaItem.IntegrityVerified = actualHash == mediaItem.Sha256Hash;
                            mediaItem.LastVerifiedAtUtc = DateTime.UtcNow;
                            if (!mediaItem.IntegrityVerified)
                            {
                                importResult.MediaItems.IntegrityIssues++;
                                importResult.Details.Add($"Hash mismatch for MediaItem {mediaItem.Id} at {mediaItem.FilePath}");
                            }
                        }
                        else
                        {
                            mediaItem.IntegrityVerified = false;
                            importResult.MediaItems.IntegrityIssues++;
                            importResult.Details.Add($"Media file missing for MediaItem {mediaItem.Id}: {mediaItem.FilePath}");
                        }

                        await ctx.MediaItems.AddAsync(mediaItem, ct);
                        importResult.MediaItems.Inserted++;
                    }

                    importResult.Success = true;
                    return importResult;
                }, ct);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup import failed");
                return new BackupImportResult { Success = false, ErrorMessage = $"Import failed: {ex.Message}" };
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }

        private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct)
        {
            if (!File.Exists(path))
            {
                return default;
            }

            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
