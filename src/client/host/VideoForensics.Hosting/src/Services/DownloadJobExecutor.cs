using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Services
{
    /// <summary>
    /// Runs a download job through the account-scoped provider services. Snapshot jobs use the same sequence as the
    /// scheduled snapshot sync: restore saved credentials, then sync for the job's range.
    /// </summary>
    public class DownloadJobExecutor : IDownloadJobExecutor
    {
        private readonly IProviderAccountRepository _accounts;
        private readonly ISnapshotSyncService _snapshots;
        private readonly IServiceProvider _services;
        private readonly ILogger<DownloadJobExecutor> _logger;

        /// <summary>Initializes a new instance of the DownloadJobExecutor.</summary>
        public DownloadJobExecutor(
            IProviderAccountRepository accounts,
            ISnapshotSyncService snapshots,
            IServiceProvider services,
            ILogger<DownloadJobExecutor> logger)
        {
            _accounts = accounts;
            _snapshots = snapshots;
            _services = services;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DownloadJobOutcome> ExecuteAsync(DownloadJob job, CancellationToken ct)
        {
            return job.Kind switch
            {
                DownloadJobKind.Snapshots => await ExecuteSnapshotsAsync(job, ct),
                // No account-scoped video service exists yet. The design doc lists this as an open decision.
                _ => Failure("Video download jobs are not wired to a provider service yet."),
            };
        }

        private async Task<DownloadJobOutcome> ExecuteSnapshotsAsync(DownloadJob job, CancellationToken ct)
        {
            ProviderAccount? account = await _accounts.GetAsync(job.ProviderAccountId, ct);
            if (account is null)
            {
                return Failure("Account not found.");
            }

            // Keyed provider services are registered under the provider name, the same key the scheduled sync uses.
            IProviderAuthService? auth = _services.GetKeyedService<IProviderAuthService>(account.ProviderName);
            if (auth is null || !await auth.RestoreFromSavedCredentialsAsync(account.Id, ct))
            {
                _logger.LogWarning("Snapshot download job skipped: saved credentials could not be restored for {ProviderName} account", account.ProviderName);
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
