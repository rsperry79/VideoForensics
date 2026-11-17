using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Services;

using Location = VideoForensics.Core.Providers.Common.Contracts.Location;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// A provider that returns no locations (for example, the Ring session is not restored yet at startup) must not wipe
    /// the cached inventory: the event pull reads that cache and does nothing when it holds no locations.
    /// </summary>
    public class InventorySyncServiceEmptyResultTests
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly ReferenceDataCache _cache = new();

        private InventorySyncService CreateSut()
        {
            return new InventorySyncService(
                _discovery.Object,
                Mock.Of<IEventAndConfigService>(),
                Mock.Of<ILocationWriteRepository>(),
                Mock.Of<IDeviceWriteRepository>(),
                _cache,
                NullLogger<InventorySyncService>.Instance);
        }

        private AccountReferenceData Seed()
        {
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var data = new AccountReferenceData(
                [new CachedLocation(locationId, "location-123", "Home")],
                new Dictionary<string, CachedDevice>(StringComparer.Ordinal)
                {
                    ["device-123"] = new CachedDevice(deviceId, locationId, "location-123", "device-123", "Front Door")
                });
            _cache.Replace(_accountId, data);
            return data;
        }

        [Fact]
        public async Task SyncAsync_ProviderReturnsNoLocations_KeepsCachedInventory()
        {
            AccountReferenceData seeded = Seed();
            _ = _discovery.Setup(d => d.GetLocationsAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Location>());

            _ = await CreateSut().SyncAsync(_accountId, CancellationToken.None);

            Assert.Same(seeded, _cache.Get(_accountId));
        }

        [Fact]
        public async Task SyncAsync_ProviderReturnsNoLocationsAndCacheEmpty_LeavesCacheUnset()
        {
            // An unset cache makes the event pull load the stored inventory from the database; an empty entry would hide it.
            _ = _discovery.Setup(d => d.GetLocationsAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Location>());

            _ = await CreateSut().SyncAsync(_accountId, CancellationToken.None);

            Assert.Null(_cache.Get(_accountId));
        }
    }
}
