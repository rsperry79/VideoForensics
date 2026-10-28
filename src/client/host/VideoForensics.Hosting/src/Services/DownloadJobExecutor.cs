using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Runs a download job through the account-scoped provider services. Both kinds use the same sequence as the
    /// scheduled sync: restore saved credentials, then run the account-scoped pull for the job's range. Video jobs
    /// use the event pull, which also downloads each event's clips.
    /// </summary>
    public class DownloadJobExecutor : IDownloadJobExecutor
    {
        private readonly IProviderAccountRepository _accounts;
        private readonly ISnapshotSyncService _snapshots;
        private readonly IEventPullService _events;
        private readonly IServiceProvider _services;
        private readonly ILogger<DownloadJobExecutor> _logger;

        /// <summary>Initializes a new instance of the DownloadJobExecutor.</summary>
        public DownloadJobExecutor(
            IProviderAccountRepository accounts,
            ISnapshotSyncService snapshots,
            IEventPullService events,
            IServiceProvider services,
            ILogger<DownloadJobExecutor> logger)
        {
            _accounts = accounts;
            _snapshots = snapshots;
            _events = events;
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
            ProviderAccount? account = await _accounts.GetAsync(job.ProviderAccountId, ct);
            if (account is null)
            {
                return Failure("Account not found.");
            }

            if (!await RestoreCredentialsAsync(account, ct))
            {
                return Failure("Saved credentials could not be restored.");
            }

            // A provider-keyed event pull is bound to that provider's services, as in AccountSyncRunner. The unkeyed one is the fallback.
            IEventPullService events = _services.GetKeyedService<IEventPullService>(account.ProviderName) ?? _events;
            EventPullResult result = await events.PullAccountEventsAsync(job.ProviderAccountId, job.StartDateUtc, job.EndDateUtc, ct);
            // The event pull records a failed device feed on the account and keeps going, so report the count here.
            string? error = result.FeedsFailed > 0 ? $"{result.FeedsFailed} device feed(s) failed." : null;
            return new DownloadJobOutcome(
                FilesDownloaded: result.MediaFilesDownloaded,
                FilesSkipped: 0,
                FilesFailed: result.FeedsFailed,
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
