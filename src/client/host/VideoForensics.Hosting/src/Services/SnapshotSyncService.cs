using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Utilities;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
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

            var targets = new List<(DeviceEntity Device, string ProviderLocationId, string LocationName)>();
            var seen = new HashSet<string>();
            foreach (var location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                foreach (DeviceEntity device in await _devices.GetByLocationIdAsync(location.Id, cancellationToken))
                {
                    if (seen.Add(device.ProviderDeviceId))
                    {
                        targets.Add((device, location.ProviderLocationId, location.Name));
                    }
                }
            }

            if (targets.Count == 0)
            {
                _logger.LogInformation("Snapshot sync for {AccountName}: no devices to snapshot", accountName);
                return default;
            }

            StorageSettings settings = await _storage.GetStatusAsync(cancellationToken);
            string? mediaRoot = settings.Categories.FirstOrDefault(c => c.Category == StorageCategory.Media)?.CurrentPath;
            if (string.IsNullOrWhiteSpace(mediaRoot))
            {
                const string message = "Media storage location is not configured";
                _logger.LogError("Snapshot sync for {AccountName} failed: {Reason}", accountName, message);
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
                _logger.LogError("Snapshot sync for {AccountName} finished with failures: {Error}", accountName, error);
            }

            return new SnapshotSyncResult(targets.Count, succeeded, saved, failed, error);
        }
    }
}
