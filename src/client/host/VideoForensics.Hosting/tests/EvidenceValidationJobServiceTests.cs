using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Tests
{
    public sealed class EvidenceValidationJobServiceTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private static EvidenceValidationJobService CreateService(Mock<IEvidenceValidationService> validation)
        {
            ServiceCollection services = new();
            _ = services.AddSingleton(validation.Object);
            ServiceProvider provider = services.BuildServiceProvider();
            return new EvidenceValidationJobService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<EvidenceValidationJobService>.Instance);
        }

        private static MediaVerificationResult Result(string fileName, string status, string? reason = null)
        {
            return new()
            {
                MediaItemId = Guid.NewGuid(),
                FileName = fileName,
                Status = status,
                FailureReason = reason
            };
        }

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
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns((Guid? _, CancellationToken _, IProgress<string>? _) => gate.Task);
            EvidenceValidationJobService service = CreateService(validation);

            EvidenceValidationJob job = await service.StartIntegrityAsync(null, CancellationToken.None);

            Assert.Equal(EvidenceValidationJobKind.Integrity, job.Kind);
            Assert.Equal(EvidenceValidationJobState.Running, job.State);
            Assert.NotEqual(Guid.Empty, job.Id);
            Assert.Null(job.CompletedUtc);

            gate.SetResult(Array.Empty<MediaVerificationResult>());
            _ = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
        }

        [Fact]
        public async Task StartIntegrityAsync_WhileRunning_ExposesProgressLineBeforeCompletion()
        {
            // The fake reports one progress line, then blocks until the test releases it. The line must be
            // visible in the job log while State is still Running, not appended only after the run returns.
            TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource reported = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns(async (Guid? _, CancellationToken _, IProgress<string>? progress) =>
                {
                    progress!.Report("a.mp4: checking");
                    reported.SetResult();
                    await gate.Task;
                    return Array.Empty<MediaVerificationResult>();
                });
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityAsync(null, CancellationToken.None);
            await reported.Task.WaitAsync(WaitTimeout);

            EvidenceValidationJob? midRun = await service.GetLatestAsync(EvidenceValidationJobKind.Integrity, CancellationToken.None);
            Assert.NotNull(midRun);
            Assert.Equal(EvidenceValidationJobState.Running, midRun!.State);
            Assert.Contains(midRun.Log, line => line.EndsWith("a.mp4: checking", StringComparison.Ordinal));

            gate.SetResult();
            _ = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
        }

        [Fact]
        public async Task StartIntegrityAsync_WhenCompleted_SetsSucceededWithResultsAndLog()
        {
            // The fake streams per-file lines through the progress sink, as the real orchestrator does.
            List<MediaVerificationResult> results =
            [
                Result("a.mp4", "verified"),
                Result("b.mp4", "failed", "hash mismatch")
            ];
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns(async (Guid? _, CancellationToken _, IProgress<string>? progress) =>
                {
                    progress!.Report("a.mp4: verified");
                    progress.Report("b.mp4: failed - hash mismatch");
                    return results;
                });
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);

            Assert.Equal(EvidenceValidationJobState.Succeeded, done.State);
            _ = Assert.NotNull(done.CompletedUtc);
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
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ThrowsAsync(new InvalidOperationException("disk unavailable"));
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);

            Assert.Equal(EvidenceValidationJobState.Failed, done.State);
            Assert.Equal("disk unavailable", done.ErrorMessage);
            _ = Assert.NotNull(done.CompletedUtc);
            Assert.Contains(done.Log, line => line.Contains("disk unavailable", StringComparison.Ordinal));
        }

        [Fact]
        public async Task StartIntegrityAsync_WhenAlreadyRunning_DoesNotStartSecondRun()
        {
            TaskCompletionSource<IReadOnlyList<MediaVerificationResult>> gate = new();
            TaskCompletionSource invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns((Guid? _, CancellationToken _, IProgress<string>? _) =>
                {
                    _ = invoked.TrySetResult();
                    return gate.Task;
                });
            EvidenceValidationJobService service = CreateService(validation);

            EvidenceValidationJob first = await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob second = await service.StartIntegrityAsync(null, CancellationToken.None);
            await invoked.Task.WaitAsync(WaitTimeout);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(EvidenceValidationJobState.Running, second.State);
            validation.Verify(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Once);

            gate.SetResult(Array.Empty<MediaVerificationResult>());
            _ = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
        }

        [Fact]
        public async Task GetLatestAsync_AfterReconcileStarted_ReturnsReconcileJobNotIntegrity()
        {
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync(Array.Empty<MediaVerificationResult>());
            _ = validation.Setup(v => v.ReconcileWithProviderAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync([]);
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityAsync(null, CancellationToken.None);
            _ = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Integrity);
            EvidenceValidationJob reconcile = await service.StartReconcileAsync(
                Guid.NewGuid(), "provider-1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);
            _ = await WaitForCompletionAsync(service, EvidenceValidationJobKind.Reconcile);

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
