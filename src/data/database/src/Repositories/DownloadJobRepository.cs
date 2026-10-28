using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for DownloadJob rows.</summary>
    public class DownloadJobRepository : IDownloadJobRepository
    {
        private const int MaxTake = 500;
        private const int MaxErrorLength = 1024;

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DownloadJobRepository> _logger;

        /// <summary>Initializes a new instance of the DownloadJobRepository.</summary>
        public DownloadJobRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DownloadJobRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task EnqueueAsync(DownloadJob job, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.DownloadJobs.Add(job);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Queued {Kind} download job {JobId} for account {AccountId}", job.Kind, job.Id, job.ProviderAccountId);
        }

        /// <inheritdoc />
        public async Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
        }

        /// <inheritdoc />
        public async Task<DownloadJob?> ClaimNextQueuedAsync(CancellationToken ct)
        {
            // The read and the state change share one transaction, so two runners cannot both claim the same row.
            // SQLite serialises writers, which is what makes this safe on the current database.
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

            DownloadJob? next = await db.DownloadJobs
                .Where(j => j.State == DownloadJobState.Queued)
                .OrderBy(j => j.CreatedUtc)
                .FirstOrDefaultAsync(ct);

            if (next is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            next.State = DownloadJobState.Running;
            next.StartedUtc = DateTime.UtcNow;
            _ = await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return next;
        }

        /// <inheritdoc />
        public async Task<bool> CompleteAsync(Guid id, int filesDownloaded, int filesSkipped, int filesFailed, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            int rows = await db.DownloadJobs
                .Where(j => j.Id == id && j.State == DownloadJobState.Running)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.State, DownloadJobState.Completed)
                    .SetProperty(j => j.FinishedUtc, DateTime.UtcNow)
                    .SetProperty(j => j.FilesDownloaded, filesDownloaded)
                    .SetProperty(j => j.FilesSkipped, filesSkipped)
                    .SetProperty(j => j.FilesFailed, filesFailed), ct);
            return rows > 0;
        }

        /// <inheritdoc />
        public async Task FailAsync(Guid id, string errorMessage, CancellationToken ct)
        {
            // Keep the stored message within the column limit configured in DownloadJobConfiguration.
            string trimmed = errorMessage.Length > MaxErrorLength ? errorMessage[..MaxErrorLength] : errorMessage;
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = await db.DownloadJobs
                .Where(j => j.Id == id && j.State == DownloadJobState.Running)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.State, DownloadJobState.Failed)
                    .SetProperty(j => j.FinishedUtc, DateTime.UtcNow)
                    .SetProperty(j => j.ErrorMessage, trimmed), ct);
        }

        /// <inheritdoc />
        public async Task<bool> CancelAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            int rows = await db.DownloadJobs
                .Where(j => j.Id == id && (j.State == DownloadJobState.Queued || j.State == DownloadJobState.Running))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.State, DownloadJobState.Cancelled)
                    .SetProperty(j => j.FinishedUtc, DateTime.UtcNow), ct);
            return rows > 0;
        }

        /// <inheritdoc />
        public async Task<int> RequeueRunningAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            int rows = await db.DownloadJobs
                .Where(j => j.State == DownloadJobState.Running)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.State, DownloadJobState.Queued)
                    .SetProperty(j => j.StartedUtc, (DateTime?)null), ct);
            if (rows > 0)
            {
                _logger.LogInformation("Requeued {Count} download job(s) left running at startup", rows);
            }

            return rows;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DownloadJob>> ListForAccountAsync(Guid accountId, int take, CancellationToken ct)
        {
            if (take is < 1 or > MaxTake)
            {
                throw new ArgumentOutOfRangeException(nameof(take), take, $"Take must be between 1 and {MaxTake}.");
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DownloadJobs
                .AsNoTracking()
                .Where(j => j.ProviderAccountId == accountId)
                .OrderByDescending(j => j.CreatedUtc)
                .Take(take)
                .ToListAsync(ct);
        }
    }
}
