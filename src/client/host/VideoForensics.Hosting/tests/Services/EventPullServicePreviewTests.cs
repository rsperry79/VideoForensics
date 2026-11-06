using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

using MediaItemEntity = VideoForensics.Data.Common.Entities.MediaItem;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>Preview generation runs after a successful media download and must never fail the device pull.</summary>
    public class EventPullServicePreviewTests : IDisposable
    {
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _download = new();
        private readonly Mock<IMediaItemRepository> _mediaItems = new();
        private readonly Mock<IMediaPreviewService> _previews = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "evp-preview-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_mediaRoot))
            {
                Directory.Delete(_mediaRoot, recursive: true);
            }
        }

        private EventPullService Sut()
        {
            return new(
                _events.Object,
                _devices.Object,
                _locations.Object,
                _accounts.Object,
                logger: null,
                mediaDownloadService: _download.Object,
                mediaRoot: _mediaRoot,
                previewService: _previews.Object,
                mediaItems: _mediaItems.Object);
        }

        private void SetupAccount()
        {
            _ = _accounts
                .Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true });
        }

        private DeviceEntity WireOneDevice()
        {
            SetupAccount();
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            var device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "dev-a", Name = "dev-a", Type = "doorbell" };
            DiscoveryStub.Wire(_accountId, location, new[] { device }, _discovery, _locations, _devices);

            _ = _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent> { new("e1", "dev-a", "motion", DateTime.UtcNow.AddMinutes(-5)) });
            _ = _events.Setup(s => s.GetDeviceConfigAsync(_accountId, "dev-a", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeviceConfig("dev-a", true, 50, "motion"));

            _ = _download.Setup(d => d.DownloadVideosAsync(
                    "dev-a", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));

            return device;
        }

        private static MediaItemEntity Clip(Guid deviceId, string name, bool purged = false)
        {
            return new MediaItemEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = name,
                FilePath = Path.Combine("media", name),
                MediaFormat = "mp4",
                Sha256Hash = name,
                RecordedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                IsPurged = purged
            };
        }

        [Fact]
        public async Task PullDevice_MediaDownloadSucceeds_RequestsPreviewForEachUnpurgedClip()
        {
            DeviceEntity device = WireOneDevice();
            _ = _mediaItems.Setup(m => m.GetByDeviceAndDateRangeAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItemEntity> { Clip(device.Id, "a.mp4"), Clip(device.Id, "b.mp4"), Clip(device.Id, "gone.mp4", purged: true) });
            _ = _previews.Setup(p => p.EnsurePreviewAsync(It.IsAny<MediaItemEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _ = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _previews.Verify(p => p.EnsurePreviewAsync(It.IsAny<MediaItemEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PullDevice_PreviewThrows_PullStillCountsDeviceAsSynced()
        {
            DeviceEntity device = WireOneDevice();
            _ = _mediaItems.Setup(m => m.GetByDeviceAndDateRangeAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItemEntity> { Clip(device.Id, "a.mp4") });
            _ = _previews.Setup(p => p.EnsurePreviewAsync(It.IsAny<MediaItemEntity>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ffmpeg exploded"));

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(new EventPullResult(DevicesSynced: 1, LocationsSynced: 1, EventsSynced: 1, ConfigAreasSynced: 0, FeedsFailed: 0), result);
            Assert.NotNull(device.LastSuccessfulPullAtUtc);
        }

        [Fact]
        public async Task PullDevice_MediaDownloadFails_NoPreviewRequested()
        {
            DeviceEntity device = WireOneDevice();
            _ = _download.Setup(d => d.DownloadVideosAsync(
                    "dev-a", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: false, ErrorMessage: "provider down"));
            _ = _mediaItems.Setup(m => m.GetByDeviceAndDateRangeAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItemEntity> { Clip(device.Id, "a.mp4") });

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(1, result.FeedsFailed);
            _previews.Verify(p => p.EnsurePreviewAsync(It.IsAny<MediaItemEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
