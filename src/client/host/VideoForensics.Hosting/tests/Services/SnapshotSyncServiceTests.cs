using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>The snapshot sync service runs the capture cycle for Ring accounts.</summary>
    public class SnapshotSyncServiceTests
    {
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceWriteRepository> _devices = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly LocationEntity _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };

        public SnapshotSyncServiceTests()
        {
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true });
            _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { _location });
        }

        [Fact]
        public async Task RunCycleAsync_EnabledDeviceWithNoRecentAttempt_CapturesOnce()
        {
            // Same fixture as SnapshotCycle_EnabledDeviceWithNoRecentAttempt_CapturesOnce, driven through the sync service.
            var capture = new Mock<IProviderSnapshotCapture>();
            _ = capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, [0xFF, 0xD8, 0xFF, 0xE0], null));
            _ = _accounts.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true } });
            _ = _devices.Setup(r => r.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "12345", Name = "Front", Type = "doorbot", SnapshotCaptureEnabled = true } });

            string mediaRoot = Path.Combine(Path.GetTempPath(), "vf-sync-cycle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new Mock<IStorageLocationProvider>();
                _ = storage.Setup(s => s.GetDefaultRoot(StorageCategory.Media)).Returns(mediaRoot);
                _ = storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>())).Returns(mediaRoot);
                var captureRows = new Mock<ISnapshotCaptureRepository>();
                _ = captureRows.Setup(r => r.GetDeviceIdsWithAttemptsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlySet<Guid>)new HashSet<Guid>());
                var auth = new Mock<IProviderAuthService>();
                _ = auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                var sessions = new Mock<ISessionProvider>();

                var services = new ServiceCollection();
                _ = services.AddSingleton(_accounts.Object);
                _ = services.AddSingleton(_locations.Object);
                _ = services.AddSingleton(_devices.Object);
                _ = services.AddSingleton(captureRows.Object);
                _ = services.AddSingleton(Mock.Of<ISyncScheduleRepository>());
                _ = services.AddSingleton(Mock.Of<IMediaItemRepository>());
                _ = services.AddSingleton(sessions.Object);
                _ = services.AddKeyedSingleton<IProviderSnapshotCapture>("Ring", capture.Object);
                _ = services.AddKeyedSingleton<IProviderAuthService>("Ring", auth.Object);
                _ = services.AddKeyedSingleton<IProviderDeviceStatus>("Ring", new RingDeviceOnlineStatus());
                _ = services.AddSingleton(new SnapshotCycle(Mock.Of<ILogger<SnapshotCycle>>(), TimeProvider.System, storage.Object, new ForensicsConfiguration()));

                using ServiceProvider provider = services.BuildServiceProvider();
                ISnapshotSyncService sut = new SnapshotSyncService(
                    provider, _accounts.Object, Mock.Of<ILogger<SnapshotSyncService>>());

                await sut.RunCycleAsync(CancellationToken.None);
            }
            finally
            {
                if (Directory.Exists(mediaRoot))
                {
                    Directory.Delete(mediaRoot, recursive: true);
                }
            }

            capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
