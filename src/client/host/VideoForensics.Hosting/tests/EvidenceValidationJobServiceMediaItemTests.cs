using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>One integrity job over a single media item: only that item's file is verified, never a whole device.</summary>
    public sealed class EvidenceValidationJobServiceMediaItemTests
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

        private static async Task<EvidenceValidationJob> WaitForCompletionAsync(EvidenceValidationJobService service)
        {
            using CancellationTokenSource timeout = new(WaitTimeout);
            while (true)
            {
                EvidenceValidationJob? latest = await service.GetLatestAsync(EvidenceValidationJobKind.Integrity, CancellationToken.None);
                if (latest is not null && latest.State != EvidenceValidationJobState.Running)
                {
                    return latest;
                }

                await Task.Delay(10, timeout.Token);
            }
        }

        [Fact]
        public async Task StartIntegrityForMediaItemAsync_VerifiesOnlyThatItem_NeverAWholeDevice()
        {
            var item = Guid.NewGuid();
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityForMediaItemAsync(item, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync([Result("clip.mp4", "missing")]);
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForMediaItemAsync(item, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service);

            Assert.Equal(EvidenceValidationJobState.Succeeded, done.State);
            Assert.NotNull(done.IntegrityResults);
            Assert.Single(done.IntegrityResults!);
            validation.Verify(v => v.VerifyLocalIntegrityForMediaItemAsync(item, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Once);
            validation.Verify(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Never);
        }

        [Fact]
        public async Task StartIntegrityForMediaItemAsync_WhileADifferentItemRuns_Throws()
        {
            var running = Guid.NewGuid();
            var other = Guid.NewGuid();
            TaskCompletionSource<IReadOnlyList<MediaVerificationResult>> gate = new();
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityForMediaItemAsync(running, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns((Guid _, CancellationToken _, IProgress<string>? _) => gate.Task);
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForMediaItemAsync(running, CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.StartIntegrityForMediaItemAsync(other, CancellationToken.None));

            gate.SetResult(Array.Empty<MediaVerificationResult>());
            _ = await WaitForCompletionAsync(service);
        }
    }
}
