using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>
    /// The runner claims one queued job, runs it through the executor, and records the outcome. The executor maps a
    /// job to the account-scoped provider service for its kind.
    /// </summary>
    public class DownloadJobRunnerTests
    {
        [Fact]
        public async Task DownloadJobRunner_RunNextJob_NoQueuedJob_ReturnsFalseAndNeverExecutes()
        {
            var repository = new Mock<IDownloadJobRepository>();
            var executor = new Mock<IDownloadJobExecutor>();
            repository.Setup(r => r.ClaimNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync((DownloadJob?)null);

            bool ran = await CreateRunner().RunNextJobAsync(repository.Object, executor.Object, CancellationToken.None);

            Assert.False(ran);
            executor.Verify(e => e.ExecuteAsync(It.IsAny<DownloadJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DownloadJobRunner_RunNextJob_ExecutorSucceeds_CompletesWithCounters()
        {
            DownloadJob job = BuildJob(DownloadJobKind.Snapshots);
            var repository = new Mock<IDownloadJobRepository>();
            repository.Setup(r => r.ClaimNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(job);
            var executor = new Mock<IDownloadJobExecutor>();
            executor.Setup(e => e.ExecuteAsync(job, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadJobOutcome(FilesDownloaded: 4, FilesSkipped: 1, FilesFailed: 0, ErrorMessage: null));

            bool ran = await CreateRunner().RunNextJobAsync(repository.Object, executor.Object, CancellationToken.None);

            Assert.True(ran);
            repository.Verify(r => r.CompleteAsync(job.Id, 4, 1, 0, It.IsAny<CancellationToken>()), Times.Once);
            repository.Verify(r => r.FailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DownloadJobRunner_RunNextJob_ExecutorReportsError_FailsWithThatMessage()
        {
            DownloadJob job = BuildJob(DownloadJobKind.Snapshots);
            var repository = new Mock<IDownloadJobRepository>();
            repository.Setup(r => r.ClaimNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(job);
            var executor = new Mock<IDownloadJobExecutor>();
            executor.Setup(e => e.ExecuteAsync(job, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadJobOutcome(0, 0, 0, "Saved credentials could not be restored."));

            _ = await CreateRunner().RunNextJobAsync(repository.Object, executor.Object, CancellationToken.None);

            repository.Verify(r => r.FailAsync(job.Id, "Saved credentials could not be restored.", It.IsAny<CancellationToken>()), Times.Once);
            repository.Verify(r => r.CompleteAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DownloadJobRunner_RunNextJob_ExecutorThrows_FailsJobAndReturnsTrue()
        {
            DownloadJob job = BuildJob(DownloadJobKind.Videos);
            var repository = new Mock<IDownloadJobRepository>();
            repository.Setup(r => r.ClaimNextQueuedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(job);
            var executor = new Mock<IDownloadJobExecutor>();
            executor.Setup(e => e.ExecuteAsync(job, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("provider unreachable"));

            bool ran = await CreateRunner().RunNextJobAsync(repository.Object, executor.Object, CancellationToken.None);

            Assert.True(ran);
            repository.Verify(r => r.FailAsync(job.Id, "provider unreachable", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DownloadJobExecutor_Videos_NotWiredYet_ReturnsErrorOutcome()
        {
            // Video jobs have no account-scoped provider service yet. This is the open decision in the design doc.
            DownloadJobExecutor executor = CreateExecutor(new Mock<IProviderAccountRepository>().Object, new Mock<ISnapshotSyncService>().Object);

            DownloadJobOutcome outcome = await executor.ExecuteAsync(BuildJob(DownloadJobKind.Videos), CancellationToken.None);

            Assert.NotNull(outcome.ErrorMessage);
            Assert.Contains("not wired", outcome.ErrorMessage);
        }

        [Fact]
        public async Task DownloadJobExecutor_Snapshots_AccountMissing_ReturnsErrorOutcome()
        {
            var accounts = new Mock<IProviderAccountRepository>();
            accounts.Setup(a => a.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);
            DownloadJobExecutor executor = CreateExecutor(accounts.Object, new Mock<ISnapshotSyncService>().Object);

            DownloadJobOutcome outcome = await executor.ExecuteAsync(BuildJob(DownloadJobKind.Snapshots), CancellationToken.None);

            Assert.Equal("Account not found.", outcome.ErrorMessage);
        }

        [Fact]
        public async Task DownloadJobExecutor_Snapshots_Succeeds_MapsSnapshotCountsToOutcome()
        {
            DownloadJob job = BuildJob(DownloadJobKind.Snapshots);
            var accounts = new Mock<IProviderAccountRepository>();
            accounts.Setup(a => a.GetAsync(job.ProviderAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccount { Id = job.ProviderAccountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow });
            var auth = new Mock<IProviderAuthService>();
            auth.Setup(a => a.RestoreFromSavedCredentialsAsync(job.ProviderAccountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var snapshots = new Mock<ISnapshotSyncService>();
            snapshots.Setup(s => s.SyncAccountSnapshotsAsync(job.ProviderAccountId, job.StartDateUtc, job.EndDateUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotSyncResult(DevicesAttempted: 3, DevicesSucceeded: 2, SnapshotsSaved: 5, DevicesFailed: 1));
            DownloadJobExecutor executor = CreateExecutor(accounts.Object, snapshots.Object, ("Ring", auth.Object));

            DownloadJobOutcome outcome = await executor.ExecuteAsync(job, CancellationToken.None);

            Assert.Null(outcome.ErrorMessage);
            Assert.Equal(5, outcome.FilesDownloaded);
            Assert.Equal(1, outcome.FilesFailed);
        }

        private static DownloadJobRunner CreateRunner()
        {
            IServiceScopeFactory scopes = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            return new DownloadJobRunner(scopes, NullLogger<DownloadJobRunner>.Instance);
        }

        private static DownloadJobExecutor CreateExecutor(IProviderAccountRepository accounts, ISnapshotSyncService snapshots, params (string Key, IProviderAuthService Service)[] keyedAuth)
        {
            var services = new ServiceCollection();
            foreach ((string key, IProviderAuthService service) in keyedAuth)
            {
                _ = services.AddKeyedSingleton(key, service);
            }

            return new DownloadJobExecutor(accounts, snapshots, services.BuildServiceProvider(), NullLogger<DownloadJobExecutor>.Instance);
        }

        private static DownloadJob BuildJob(DownloadJobKind kind)
        {
            return new DownloadJob
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = Guid.NewGuid(),
                Kind = kind,
                StartDateUtc = DateTime.UtcNow.AddDays(-1),
                EndDateUtc = DateTime.UtcNow,
                State = DownloadJobState.Running,
                CreatedUtc = DateTime.UtcNow,
                RequestedBy = Guid.NewGuid(),
            };
        }
    }
}
