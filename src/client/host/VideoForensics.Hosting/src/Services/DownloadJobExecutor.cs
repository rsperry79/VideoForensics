using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Runs a download job through the account-scoped provider services. Both kinds restore saved credentials first.
    /// Snapshot jobs then run the snapshot sync for the range. Video jobs download every clip in the range, one device
    /// at a time, into a subfolder of the job's output folder.
    /// </summary>
    public class DownloadJobExecutor : IDownloadJobExecutor
    {
        private readonly IProviderAccountRepository _accounts;
        private readonly ISnapshotSyncService _snapshots;
        private readonly IAccountDeviceRepository _devices;
        private readonly ILocationRepository _locations;
        private readonly IServiceProvider _services;
        private readonly ILogger<DownloadJobExecutor> _logger;

        /// <summary>Initializes a new instance of the DownloadJobExecutor.</summary>
        public DownloadJobExecutor(
            IProviderAccountRepository accounts,
            ISnapshotSyncService snapshots,
            IAccountDeviceRepository devices,
            ILocationRepository locations,
            IServiceProvider services,
            ILogger<DownloadJobExecutor> logger)
        {
            _accounts = accounts;
            _snapshots = snapshots;
            _devices = devices;
            _locations = locations;
            _services = services;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DownloadJobOutcome> ExecuteAsync(DownloadJob job, CancellationToken ct)
        {
            return job.Kind switch
            {
                DownloadJobKind.Snapshots => await ExecuteSnapshotsAsync(job, ct),
                DownloadJobKind.Videos => await ExecuteVideosAsync(job, ct),
                _ => Failure($"Unknown download job kind: {job.Kind}."),
            };
        }

        private async Task<DownloadJobOutcome> ExecuteVideosAsync(DownloadJob job, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(job.OutputPath))
            {
                return Failure("Output folder is required for video jobs.");
            }

            ProviderAccount? account = await _accounts.GetAsync(job.ProviderAccountId, ct);
            if (account is null)
            {
                return Failure("Account not found.");
            }

            if (!await RestoreCredentialsAsync(account, ct))
            {
                return Failure("Saved credentials could not be restored.");
            }

            IMediaDownloadService? media = _services.GetKeyedService<IMediaDownloadService>(account.ProviderName);
            if (media is null)
            {
                return Failure($"No media download service is registered for {account.ProviderName}.");
            }

            // Some providers (Ring) cache the account from this call, so it must run before the first download.
            media.SetActiveProviderAccountId(account.Id);

            int filesDownloaded = 0;
            int devicesFailed = 0;
            IReadOnlyList<Device> devices = await _devices.ListByProviderAccountAsync(account.Id, ct);
            foreach (Device device in devices)
            {
                // Attribution uses the device's real location, so the provider does not fall back to a placeholder.
                string? providerLocationId = (await _locations.GetAsync(device.LocationId, ct))?.ProviderLocationId;
                string deviceFolder = Path.Combine(job.OutputPath, device.ProviderDeviceId);
                _ = Directory.CreateDirectory(deviceFolder);

                DownloadResult result = await media.DownloadVideosAsync(
                    device.ProviderDeviceId, deviceFolder, job.StartDateUtc, job.EndDateUtc, providerLocationId, ct);
                if (result.Success)
                {
                    filesDownloaded += result.FilesDownloaded;
                }
                else
                {
                    // One failed device does not stop the others. The count is reported on the job.
                    devicesFailed++;
                    _logger.LogWarning("Video download for device {DeviceName} failed: {ErrorMessage}", device.Name, result.ErrorMessage);
                }
            }

            string? error = devicesFailed > 0 ? $"{devicesFailed} device(s) failed." : null;
            if (error is null && filesDownloaded > 0)
            {
                // The watermark moves only when the whole job succeeded and produced files, matching the manual download rule.
                account.LastDownloadTimeUtc = job.EndDateUtc;
                await _accounts.UpdateAsync(account, ct);
            }

            return new DownloadJobOutcome(
                FilesDownloaded: filesDownloaded,
                FilesSkipped: 0,
                FilesFailed: devicesFailed,
                ErrorMessage: error);
        }

        private async Task<bool> RestoreCredentialsAsync(ProviderAccount account, CancellationToken ct)
        {
            // Keyed provider services are registered under the provider name, the same key the scheduled sync uses.
            IProviderAuthService? auth = _services.GetKeyedService<IProviderAuthService>(account.ProviderName);
            if (auth is not null && await auth.RestoreFromSavedCredentialsAsync(account.Id, ct))
            {
                return true;
            }

            _logger.LogWarning("Download job skipped: saved credentials could not be restored for {ProviderName} account", account.ProviderName);
            return false;
        }

        private async Task<DownloadJobOutcome> ExecuteSnapshotsAsync(DownloadJob job, CancellationToken ct)
        {
            ProviderAccount? account = await _accounts.GetAsync(job.ProviderAccountId, ct);
            if (account is null)
            {
                return Failure("Account not found.");
            }

            if (!await RestoreCredentialsAsync(account, ct))
            {
                return Failure("Saved credentials could not be restored.");
            }

            SnapshotSyncResult result = await _snapshots.SyncAccountSnapshotsAsync(job.ProviderAccountId, job.StartDateUtc, job.EndDateUtc, ct);
            return new DownloadJobOutcome(
                FilesDownloaded: result.SnapshotsSaved,
                FilesSkipped: 0,
                FilesFailed: result.DevicesFailed,
                ErrorMessage: result.ErrorMessage);
        }

        private static DownloadJobOutcome Failure(string message)
        {
            return new DownloadJobOutcome(0, 0, 0, message);
        }
    }
}
