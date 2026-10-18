using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public sealed class EvidenceValidationJobServiceTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private static EvidenceValidationJobService CreateService(Mock<IEvidenceValidationService> validation)
        {
            ServiceCollection services = new();
            services.AddSingleton(validation.Object);
            ServiceProvider provider = services.BuildServiceProvider();
            return new EvidenceValidationJobService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<EvidenceValidationJobService>.Instance);
        }

        private static MediaVerificationResult Result(string fileName, string status, string? reason = null) => new()
        {
            MediaItemId = Guid.NewGuid(),
            FileName = fileName,
            Status = status,
            FailureReason = reason
        };

        private static async Task<EvidenceValidationJob> WaitForCompletionAsync(EvidenceValidationJobService service, EvidenceValidationJobKind kind)
        {
            using CancellationTokenSource timeout = new(WaitTimeout);
            while (true)
            {
                EvidenceValidationJob? latest = await service.GetLatestAsync(kind, CancellationToken.None);
                if (latest is not null && latest.State != EvidenceValidationJobState.Running)
                {
                    return latest;
                }

                await Task.Delay(10, timeout.Token);
            }
        }

        [Fact]
        public async Task StartIntegrityAsync_WhileNothingRunning_ReturnsRunningJob()
        {
            TaskCompletionSource<IReadOnlyList<MediaVerificationResult>> gate = new();
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns((Guid? _, CancellationToken _) => gate.Task);
            EvidenceValidationJobService service = CreateService(validation);

            EvidenceValidationJob job = await service.StartIntegrityAsync(null, CancellationToken.None);

            Assert.Equal(EvidenceValidationJobKind.Integrity, job.Kind);
            Assert.Equal(EvidenceValidationJobState.Running, job.State);
            Assert.NotEqual(Guid.Empty, job.Id);
            Assert.Null(job.CompletedUtc);

            gate.SetResult(Array.Empty<MediaVerificationResult>());
            await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
        }

        [Fact]
        public async Task StartIntegrityAsync_WhenCompleted_SetsSucceededWithResultsAndLog()
        {
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaVerificationResult>
                {
                    Result("a.mp4", "verified"),
                    Result("b.mp4", "failed", "hash mismatch")
                });
            EvidenceValidationJobService service = CreateService(validation);

            await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);

            Assert.Equal(EvidenceValidationJobState.Succeeded, done.State);
            Assert.NotNull(done.CompletedUtc);
            Assert.NotNull(done.IntegrityResults);
            Assert.Equal(2, done.IntegrityResults!.Count);
            Assert.Null(done.ErrorMessage);
            Assert.Contains(done.Log, line => line.Contains("Integrity check started", StringComparison.Ordinal));
            Assert.Contains(done.Log, line => line.Contains("a.mp4: verified", StringComparison.Ordinal));
            Assert.Contains(done.Log, line => line.Contains("b.mp4: failed", StringComparison.Ordinal) && line.Contains("hash mismatch", StringComparison.Ordinal));
            Assert.Contains(done.Log, line => line.Contains("Finished: 2 results", StringComparison.Ordinal));
        }

        [Fact]
        public async Task StartIntegrityAsync_WhenServiceThrows_SetsFailedWithErrorMessage()
        {
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("disk unavailable"));
            EvidenceValidationJobService service = CreateService(validation);

            await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);

            Assert.Equal(EvidenceValidationJobState.Failed, done.State);
            Assert.Equal("disk unavailable", done.ErrorMessage);
            Assert.NotNull(done.CompletedUtc);
            Assert.Contains(done.Log, line => line.Contains("disk unavailable", StringComparison.Ordinal));
        }

        [Fact]
        public async Task StartIntegrityAsync_WhenAlreadyRunning_DoesNotStartSecondRun()
        {
            TaskCompletionSource<IReadOnlyList<MediaVerificationResult>> gate = new();
            TaskCompletionSource invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns((Guid? _, CancellationToken _) =>
                {
                    invoked.TrySetResult();
                    return gate.Task;
                });
            EvidenceValidationJobService service = CreateService(validation);

            EvidenceValidationJob first = await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob second = await service.StartIntegrityAsync(null, CancellationToken.None);
            await invoked.Task.WaitAsync(WaitTimeout);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(EvidenceValidationJobState.Running, second.State);
            validation.Verify(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);

            gate.SetResult(Array.Empty<MediaVerificationResult>());
            await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
        }

        [Fact]
        public async Task GetLatestAsync_AfterReconcileStarted_ReturnsReconcileJobNotIntegrity()
        {
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<MediaVerificationResult>());
            validation.Setup(v => v.ReconcileWithProviderAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());
            EvidenceValidationJobService service = CreateService(validation);

            await service.StartIntegrityAsync(null, CancellationToken.None);
            await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
            EvidenceValidationJob reconcile = await service.StartReconcileAsync(
                Guid.NewGuid(), "provider-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);
            await WaitForCompletionAsync(service, EvidenceValidationJobKind.Reconcile);

            EvidenceValidationJob? latestReconcile = await service.GetLatestAsync(EvidenceValidationJobKind.Reconcile, CancellationToken.None);
            EvidenceValidationJob? latestIntegrity = await service.GetLatestAsync(EvidenceValidationJobKind.Integrity, CancellationToken.None);

            Assert.NotNull(latestReconcile);
            Assert.Equal(EvidenceValidationJobKind.Reconcile, latestReconcile!.Kind);
            Assert.Equal(reconcile.Id, latestReconcile.Id);
            Assert.NotNull(latestIntegrity);
            Assert.Equal(EvidenceValidationJobKind.Integrity, latestIntegrity!.Kind);
        }

        [Fact]
        public async Task GetLatestAsync_WithNoJobs_ReturnsNull()
        {
            Mock<IEvidenceValidationService> validation = new();
            EvidenceValidationJobService service = CreateService(validation);

            EvidenceValidationJob? latest = await service.GetLatestAsync(EvidenceValidationJobKind.Integrity, CancellationToken.None);

            Assert.Null(latest);
        }
    }
}
