using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>A failing device keeps the results of the devices verified before it; the job log stays bounded.</summary>
    public sealed class EvidenceValidationJobPartialTests
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
        public async Task LaterDeviceFails_JobFailed_ButKeepsEarlierDeviceResults()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var firstResult = new MediaVerificationResult { MediaItemId = Guid.NewGuid(), FileName = "a.mp4", Status = "verified" };
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(first, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ReturnsAsync([firstResult]);
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(second, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .ThrowsAsync(new InvalidOperationException("hash store offline"));
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityForDevicesAsync(new[] { first, second }, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service);

            Assert.Equal(EvidenceValidationJobState.Failed, done.State);
            Assert.Equal("hash store offline", done.ErrorMessage);
            MediaVerificationResult kept = Assert.Single(done.IntegrityResults!);
            Assert.Equal(first, kept.DeviceId);
            Assert.Equal("a.mp4", kept.FileName);
        }

        [Fact]
        public async Task LongRun_LogKeepsNewestLinesWithinCap()
        {
            Mock<IEvidenceValidationService> validation = new();
            _ = validation.Setup(v => v.VerifyLocalIntegrityAsync(null, It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                .Returns((Guid? _, CancellationToken _, IProgress<string>? progress) =>
                {
                    for (int i = 0; i < 1500; i++)
                    {
                        progress?.Report($"file {i}");
                    }

                    return Task.FromResult<IReadOnlyList<MediaVerificationResult>>([]);
                });
            EvidenceValidationJobService service = CreateService(validation);

            _ = await service.StartIntegrityAsync(null, CancellationToken.None);
            EvidenceValidationJob done = await WaitForCompletionAsync(service);

            Assert.True(done.Log.Count <= 1001, $"log had {done.Log.Count} lines");
            Assert.Contains(done.Log, line => line.EndsWith("file 1499", StringComparison.Ordinal));
            Assert.DoesNotContain(done.Log, line => line.EndsWith("file 0", StringComparison.Ordinal));
        }
    }
}
