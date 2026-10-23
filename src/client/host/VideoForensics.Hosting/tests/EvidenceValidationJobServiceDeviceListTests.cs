using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>One integrity job over a device list: each device is verified in turn and its results are tagged.</summary>
    public sealed class EvidenceValidationJobServiceDeviceListTests
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
        public async Task StartIntegrityForDevicesAsync_VerifiesEachDeviceOnce_AndTagsEveryResult()
        {
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(first, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync(new List<MediaVerificationResult> { Result("a.mp4", "verified") });
            validation.Setup(v => v.VerifyLocalIntegrityAsync(second, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync(new List<MediaVerificationResult> { Result("b.mp4", "failed", "hash mismatch") });
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForDevicesAsync(new[] { first, second }, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service);

            Assert.Equal(EvidenceValidationJobState.Succeeded, done.State);
            Assert.NotNull(done.IntegrityResults);
            Assert.Equal(2, done.IntegrityResults!.Count);
            Assert.Equal(first, done.IntegrityResults[0].DeviceId);
            Assert.Equal(second, done.IntegrityResults[1].DeviceId);
            validation.Verify(v => v.VerifyLocalIntegrityAsync(first, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Once);
            validation.Verify(v => v.VerifyLocalIntegrityAsync(second, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Once);
        }

        [Fact]
        public async Task StartIntegrityForDevicesAsync_DuplicateDeviceIds_VerifiesEachDeviceOnce()
        {
            Guid device = Guid.NewGuid();
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(device, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync(new List<MediaVerificationResult>());
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForDevicesAsync(new[] { device, device }, CancellationToken.None);
            _ = await WaitForCompletionAsync(service);

            validation.Verify(v => v.VerifyLocalIntegrityAsync(device, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Once);
        }

        [Fact]
        public async Task StartIntegrityForDevicesAsync_WhileADifferentSetRuns_Throws_AndSameSetJoins()
        {
            Guid running = Guid.NewGuid();
            Guid other = Guid.NewGuid();
            TaskCompletionSource<IReadOnlyList<MediaVerificationResult>> gate = new();
            Mock<IEvidenceValidationService> validation = new();
            validation.Setup(v => v.VerifyLocalIntegrityAsync(running, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns(gate.Task);
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForDevicesAsync(new[] { running }, CancellationToken.None);

            // A different device set must not be handed the running job's results.
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.StartIntegrityForDevicesAsync(new[] { other }, CancellationToken.None));

            // The same set may join the running job.
            EvidenceValidationJob joined = await service.StartIntegrityForDevicesAsync(new[] { running }, CancellationToken.None);
            Assert.Equal(EvidenceValidationJobState.Running, joined.State);

            gate.SetResult(new List<MediaVerificationResult>());
            _ = await WaitForCompletionAsync(service);
        }

        [Fact]
        public async Task StartIntegrityForDevicesAsync_EmptyList_ThrowsArgumentException()
        {
            Mock<IEvidenceValidationService> validation = new();
            EvidenceValidationJobService service = CreateService(validation);

            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.StartIntegrityForDevicesAsync(Array.Empty<Guid>(), CancellationToken.None));
            validation.Verify(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()), Times.Never);
        }
    }
}
