using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Proves a pull publishes live per-file progress to the sync progress store while a device downloads, returns the
    /// store to idle when the pull ends, and that a failing status read never fails the pull. Temp folders only.
    /// </summary>
    public class EventPullServiceProgressTests : IDisposable
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-eventpull-progress-" + Guid.NewGuid().ToString("N"));
        private readonly LocationEntity _location;
        private readonly DeviceEntity _device;
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _deviceRepository = new();
        private readonly Mock<ILocationRepository> _locationRepository = new();
        private readonly Mock<IProviderAccountRepository> _accountRepository = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();

        public EventPullServiceProgressTests()
        {
            _location = new LocationEntity { Id = _locationId, ProviderLocationId = "loc-1", Name = "Home" };
            _device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };

            _ = _accountRepository.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, ProviderName = "Ring", IsActive = true });
            DiscoveryStub.Wire(_accountId, _location, new[] { _device }, _discovery, _locationRepository, _deviceRepository);

            _ = _events.Setup(s => s.GetEventsAsync(_accountId, "dev-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private EventPullService BuildSut(ISyncProgressStore? progress, TimeSpan? pollInterval = null)
        {
            return new EventPullService(
                _events.Object,
                _deviceRepository.Object,
                _locationRepository.Object,
                _accountRepository.Object,
                mediaDownloadService: _media.Object,
                mediaRoot: Path.Combine(_root, "media"),
                progress: progress,
                progressPollInterval: pollInterval ?? TimeSpan.FromMilliseconds(20));
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("Condition was not met in time.");
                }

                await Task.Delay(10);
            }
        }

        [Fact]
        public async Task EventPullService_DownloadPending_PublishesLiveProgress()
        {
            var store = new SyncProgressStore();
            var tcs = new TaskCompletionSource<DownloadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(() => tcs.Task);
            _ = _media.Setup(m => m.GetStatus()).Returns(new DownloadStatus(true, 3, 5, 1024L, "a.mp4"));

            Task<EventPullResult> pull = BuildSut(store).PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            await WaitUntilAsync(() => store.Current.CurrentFile == "a.mp4");
            SyncProgress live = store.Current;
            Assert.True(live.IsDownloading);
            Assert.Equal(3, live.FilesCompleted);
            Assert.Equal(5, live.FilesTotal);
            Assert.Equal("Front Door", live.DeviceName);
            Assert.Equal(1, live.DeviceIndex);
            Assert.Equal(1, live.DeviceTotal);

            tcs.SetResult(new DownloadResult(Success: true));
            _ = await pull;
        }

        [Fact]
        public async Task EventPullService_PullFinished_ClearsProgressToIdle()
        {
            var store = new SyncProgressStore();
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));
            _ = _media.Setup(m => m.GetStatus()).Returns(new DownloadStatus(true, 3, 5, 1024L, "a.mp4"));

            _ = await BuildSut(store).PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(SyncProgress.Idle, store.Current);
        }

        [Fact]
        public async Task EventPullService_GetStatusThrows_PullStillSucceeds()
        {
            var store = new SyncProgressStore();
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));
            _ = _media.Setup(m => m.GetStatus()).Throws(new InvalidOperationException("status unavailable"));

            EventPullResult result = await BuildSut(store).PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(0, result.FeedsFailed);
            Assert.Equal(1, result.DevicesSynced);
            _media.Verify(m => m.DownloadVideosAsync("dev-1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
