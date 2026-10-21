using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Utilities;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Hosting.Contracts;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Hosting.Services
{
    /// <inheritdoc />
    public class SnapshotSyncService : ISnapshotSyncService
    {
        private readonly IServiceProvider _services;
        private readonly IProviderAccountRepository _accounts;
        private readonly ILocationRepository _locations;
        private readonly IDeviceRepository _devices;
        private readonly IStorageSettingsService _storage;
        private readonly ILogger<SnapshotSyncService> _logger;

        /// <summary>Creates the service.</summary>
        public SnapshotSyncService(
            IServiceProvider services,
            IProviderAccountRepository accounts,
            ILocationRepository locations,
            IDeviceRepository devices,
            IStorageSettingsService storage,
            ILogger<SnapshotSyncService> logger)
        {
            _services = services;
            _accounts = accounts;
            _locations = locations;
            _devices = devices;
            _storage = storage;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SnapshotSyncResult> SyncAccountSnapshotsAsync(Guid accountId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
        {
            var account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return new SnapshotSyncResult(ErrorMessage: "Account not found");
            }

            // Human-readable name only: raw account GUIDs must not reach the logs.
            string accountName = $"{account.ProviderName} account";

            // Downloads are attributed to the account that is syncing, not whichever was active last.
            // The provider is chosen by the account's provider name: the unkeyed service is only whichever provider
            // registered last, so with several providers it can belong to the wrong one. Same keyed lookup the scheduled
            // event path uses; the unkeyed service remains the fallback when no keyed services were registered.
            IMediaDownloadService download = _services.GetKeyedService<IMediaDownloadService>(account.ProviderName)
                ?? _services.GetRequiredService<IMediaDownloadService>();
            download.SetActiveProviderAccountId(accountId);

            // The offline rule belongs to the account's provider. A provider with no registered rule keeps every device,
            // because nothing on this path can tell that it is offline.
            IProviderDeviceStatus? deviceStatus = _services.GetKeyedService<IProviderDeviceStatus>(account.ProviderName);

            var targets = new List<(DeviceEntity Device, string ProviderLocationId, string LocationName)>();
            var seen = new HashSet<string>();
            var offline = 0;
            foreach (var location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                foreach (DeviceEntity device in await _devices.GetByLocationIdAsync(location.Id, cancellationToken))
                {
                    if (!seen.Add(device.ProviderDeviceId))
                    {
                        continue;
                    }

                    // Only cameras have snapshots. Chimes, base stations and range extenders are health-only devices, and their
                    // health comes from the device health sync, so asking Ring for a snapshot would only fail.
                    if (!SnapshotCaptureService.HasCamera(device.Type))
                    {
                        continue;
                    }

                    if (deviceStatus?.IsOffline(device.Type, device.IsOnline) == true)
                    {
                        offline++;
                        continue;
                    }

                    targets.Add((device, location.ProviderLocationId, location.Name));
                }
            }

            if (offline > 0)
            {
                _logger.LogDebug("Snapshot sync for {AccountName}: skipped {OfflineCount} offline device(s)", accountName, offline);
            }

            if (targets.Count == 0)
            {
                _logger.LogDebug("Snapshot sync for {AccountName}: no devices to snapshot", accountName);
                return default;
            }

            StorageSettings settings = await _storage.GetStatusAsync(cancellationToken);
            string? mediaRoot = settings.Categories.FirstOrDefault(c => c.Category == StorageCategory.Media)?.CurrentPath;
            if (string.IsNullOrWhiteSpace(mediaRoot))
            {
                const string message = "Media storage location is not configured";
                _logger.LogWarning("Snapshot sync for {AccountName} failed: {Reason}", accountName, message);
                return new SnapshotSyncResult(ErrorMessage: message);
            }

            DateTime from = fromUtc ?? DateTime.MinValue;
            DateTime to = toUtc ?? DateTime.UtcNow;
            int succeeded = 0, saved = 0, failed = 0;
            string? firstError = null;

            foreach (var (device, providerLocationId, locationName) in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string outputPath = PathUtilities.BuildSavePath(mediaRoot, locationName, device.Name);
                try
                {
                    DownloadResult result = await download.DownloadSnapshotsAsync(
                        device.ProviderDeviceId, outputPath, from, to, providerLocationId, cancellationToken);
                    if (result.Success)
                    {
                        succeeded++;
                        saved += result.FilesDownloaded;
                    }
                    else
                    {
                        failed++;
                        firstError ??= $"{device.Name}: {result.ErrorMessage ?? "unknown error"}";
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Snapshot download failed for a device on {AccountName}", accountName);
                    failed++;
                    firstError ??= $"{device.Name}: {ex.Message}";
                }
            }

            string? error = failed > 0 ? $"{failed} of {targets.Count} device(s) failed: {firstError}" : null;
            if (error is null)
            {
                _logger.LogInformation("Snapshot sync for {AccountName} saved {SnapshotCount} snapshot(s) from {DeviceCount} device(s)", accountName, saved, succeeded);
            }
            else
            {
                _logger.LogWarning("Snapshot sync for {AccountName} finished with failures", accountName);
            }

            return new SnapshotSyncResult(targets.Count, succeeded, saved, failed, error);
        }
    }
}
