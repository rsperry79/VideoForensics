using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Proves an event pull reads the stored inventory from the reference cache and links its media to the stored
    /// location and device, without asking the provider for locations or devices.
    /// </summary>
    public class EventPullServiceReferenceCacheTests
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _eventAndConfig = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();

        [Fact]
        public async Task PullAccountEventsAsync_CachedInventory_UsesStoredLocationAndNeverAsksProviderForInventory()
        {
            CancellationToken cancellationToken = CancellationToken.None;
            var cache = new ReferenceDataCache();
            cache.Replace(_accountId, new AccountReferenceData(
                [new CachedLocation(_locationId, "location-123", "Home")],
                new Dictionary<string, CachedDevice>(StringComparer.Ordinal)
                {
                    ["device-123"] = new CachedDevice(_deviceId, _locationId, "location-123", "device-123", "Front Door")
                }));

            _ = _accounts.Setup(r => r.GetAsync(_accountId, cancellationToken)).ReturnsAsync(new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            });
            _ = _devices.Setup(r => r.GetAsync(_deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
            {
                Id = _deviceId,
                LocationId = _locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            });
            _ = _eventAndConfig.Setup(s => s.GetEventsAsync(_accountId, "device-123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));

            var sut = new EventPullService(
                _eventAndConfig.Object,
                _devices.Object,
                _locations.Object,
                _accounts.Object,
                mediaDownloadService: _media.Object,
                mediaRoot: Path.Combine(Path.GetTempPath(), "vf-refcache-" + Guid.NewGuid().ToString("N")),
                referenceCache: cache);

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, cancellationToken);

            _discovery.Verify(d => d.GetLocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _discovery.Verify(d => d.GetDevicesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

            // The media is attributed to the stored location, so the download is never sent without its location id.
            _media.Verify(m => m.DownloadVideosAsync("device-123", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), "location-123", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_EmptyCacheAndNoStoredInventory_PullsNothingAndDoesNotCallProvider()
        {
            CancellationToken cancellationToken = CancellationToken.None;
            _ = _accounts.Setup(r => r.GetAsync(_accountId, cancellationToken)).ReturnsAsync(new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            });
            _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var sut = new EventPullService(
                _eventAndConfig.Object,
                _devices.Object,
                _locations.Object,
                _accounts.Object,
                mediaDownloadService: _media.Object,
                referenceCache: new ReferenceDataCache());

            EventPullResult result = await sut.PullAccountEventsAsync(_accountId, null, null, cancellationToken);

            Assert.Equal(0, result.LocationsSynced);
            _discovery.Verify(d => d.GetLocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _eventAndConfig.Verify(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
