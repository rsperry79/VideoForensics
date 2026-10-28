using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class DownloadJobRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DownloadJobRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DownloadJobRepository(_fixture.Factory, loggerFactory.CreateLogger<DownloadJobRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task DownloadJobRepository_EnqueueAndGet_RoundTrips()
        {
            var accountId = Guid.NewGuid();
            DownloadJob job = BuildJob(accountId, DownloadJobKind.Videos, DateTime.UtcNow);

            await _repository.EnqueueAsync(job, CancellationToken.None);
            DownloadJob? retrieved = await _repository.GetAsync(job.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(accountId, retrieved.ProviderAccountId);
            Assert.Equal(DownloadJobKind.Videos, retrieved.Kind);
            Assert.Equal(DownloadJobState.Queued, retrieved.State);
            Assert.True(retrieved.Force);
        }

        [Fact]
        public async Task DownloadJobRepository_ClaimNextQueued_ReturnsOldestAndMarksRunning()
        {
            var accountId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            DownloadJob older = BuildJob(accountId, DownloadJobKind.Videos, now.AddMinutes(-5));
            DownloadJob newer = BuildJob(accountId, DownloadJobKind.Videos, now);
            await _repository.EnqueueAsync(newer, CancellationToken.None);
            await _repository.EnqueueAsync(older, CancellationToken.None);

            DownloadJob? first = await _repository.ClaimNextQueuedAsync(CancellationToken.None);
            DownloadJob? second = await _repository.ClaimNextQueuedAsync(CancellationToken.None);
            DownloadJob? third = await _repository.ClaimNextQueuedAsync(CancellationToken.None);

            Assert.NotNull(first);
            Assert.Equal(older.Id, first.Id);
            Assert.Equal(DownloadJobState.Running, first.State);
            Assert.NotNull(first.StartedUtc);
            Assert.NotNull(second);
            Assert.Equal(newer.Id, second.Id);
            Assert.Null(third);
        }

        [Fact]
        public async Task DownloadJobRepository_ClaimNextQueued_SkipsCancelledJobs()
        {
            var accountId = Guid.NewGuid();
            DownloadJob job = BuildJob(accountId, DownloadJobKind.Snapshots, DateTime.UtcNow);
            await _repository.EnqueueAsync(job, CancellationToken.None);
            _ = await _repository.CancelAsync(job.Id, CancellationToken.None);

            DownloadJob? claimed = await _repository.ClaimNextQueuedAsync(CancellationToken.None);

            Assert.Null(claimed);
        }

        [Fact]
        public async Task DownloadJobRepository_Complete_SetsStateAndCounters()
        {
            DownloadJob job = BuildJob(Guid.NewGuid(), DownloadJobKind.Videos, DateTime.UtcNow);
            await _repository.EnqueueAsync(job, CancellationToken.None);
            _ = await _repository.ClaimNextQueuedAsync(CancellationToken.None);

            await _repository.CompleteAsync(job.Id, filesDownloaded: 7, filesSkipped: 2, filesFailed: 1, CancellationToken.None);
            DownloadJob? retrieved = await _repository.GetAsync(job.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(DownloadJobState.Completed, retrieved.State);
            Assert.NotNull(retrieved.FinishedUtc);
            Assert.Equal(7, retrieved.FilesDownloaded);
            Assert.Equal(2, retrieved.FilesSkipped);
            Assert.Equal(1, retrieved.FilesFailed);
        }

        [Fact]
        public async Task DownloadJobRepository_CancelCompletedJob_ReturnsFalseAndKeepsState()
        {
            DownloadJob job = BuildJob(Guid.NewGuid(), DownloadJobKind.Videos, DateTime.UtcNow);
            await _repository.EnqueueAsync(job, CancellationToken.None);
            _ = await _repository.ClaimNextQueuedAsync(CancellationToken.None);
            await _repository.CompleteAsync(job.Id, 1, 0, 0, CancellationToken.None);

            bool cancelled = await _repository.CancelAsync(job.Id, CancellationToken.None);
            DownloadJob? retrieved = await _repository.GetAsync(job.Id, CancellationToken.None);

            Assert.False(cancelled);
            Assert.NotNull(retrieved);
            Assert.Equal(DownloadJobState.Completed, retrieved.State);
        }

        [Fact]
        public async Task DownloadJobRepository_RequeueRunning_ResetsRunningJobsToQueued()
        {
            // Simulates a restart: a job was Running when the process stopped.
            DownloadJob job = BuildJob(Guid.NewGuid(), DownloadJobKind.Videos, DateTime.UtcNow);
            await _repository.EnqueueAsync(job, CancellationToken.None);
            _ = await _repository.ClaimNextQueuedAsync(CancellationToken.None);

            int requeued = await _repository.RequeueRunningAsync(CancellationToken.None);
            DownloadJob? retrieved = await _repository.GetAsync(job.Id, CancellationToken.None);

            Assert.Equal(1, requeued);
            Assert.NotNull(retrieved);
            Assert.Equal(DownloadJobState.Queued, retrieved.State);
            Assert.Null(retrieved.StartedUtc);
        }

        [Fact]
        public async Task DownloadJobRepository_ListForAccount_FiltersByAccountAndNewestFirst()
        {
            var accountId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            await _repository.EnqueueAsync(BuildJob(accountId, DownloadJobKind.Videos, now.AddHours(-2)), CancellationToken.None);
            await _repository.EnqueueAsync(BuildJob(accountId, DownloadJobKind.Snapshots, now), CancellationToken.None);
            await _repository.EnqueueAsync(BuildJob(Guid.NewGuid(), DownloadJobKind.Videos, now), CancellationToken.None);

            IReadOnlyList<DownloadJob> list = await _repository.ListForAccountAsync(accountId, take: 10, CancellationToken.None);

            Assert.Equal(2, list.Count);
            Assert.All(list, j => Assert.Equal(accountId, j.ProviderAccountId));
            Assert.True(list[0].CreatedUtc > list[1].CreatedUtc);
        }

        private static DownloadJob BuildJob(Guid accountId, DownloadJobKind kind, DateTime createdUtc)
        {
            return new DownloadJob
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = accountId,
                Kind = kind,
                StartDateUtc = createdUtc.AddDays(-1),
                EndDateUtc = createdUtc,
                Force = true,
                State = DownloadJobState.Queued,
                CreatedUtc = createdUtc,
                RequestedBy = Guid.NewGuid(),
            };
        }
    }
}
