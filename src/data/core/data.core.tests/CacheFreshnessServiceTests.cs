using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>Enforces cache freshness logic: staleness detection, TTL tracking.</summary>
    public class CacheFreshnessServiceTests
    {
        private readonly CacheFreshnessService _service;

        public CacheFreshnessServiceTests()
        {
            var logger = new Mock<ILogger<CacheFreshnessService>>();
            _service = new CacheFreshnessService(logger.Object);
        }

        [Fact]
        public void MarkSynced_SetsLastSyncedUtcAndSyncStatus()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot"
            };

            Device marked = _service.MarkSynced(device);

            _ = Assert.NotNull(marked.LastSyncedUtc);
            Assert.Equal(SyncStatus.Synced, marked.SyncStatus);
            Assert.True((DateTime.UtcNow - marked.LastSyncedUtc.Value).TotalSeconds < 5);
        }

        [Fact]
        public void IsStale_FreshCache_ReturnsFalse()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot",
                LastSyncedUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            bool isStale = _service.IsStale(device, maxAgeMinutes: 60);
            Assert.False(isStale);
        }

        [Fact]
        public void IsStale_StaleCache_ReturnsTrue()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot",
                LastSyncedUtc = DateTime.UtcNow.AddHours(-2)
            };

            bool isStale = _service.IsStale(device, maxAgeMinutes: 60);
            Assert.True(isStale);
        }

        [Fact]
        public void IsStale_NeverSynced_ReturnsTrue()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot",
                LastSyncedUtc = null
            };

            bool isStale = _service.IsStale(device, maxAgeMinutes: 60);
            Assert.True(isStale);
        }

        [Fact]
        public void GetAgeMinutes_ReturnsCorrectAge()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot",
                LastSyncedUtc = DateTime.UtcNow.AddMinutes(-25)
            };

            int age = _service.GetAgeMinutes(device);
            Assert.True(age is >= 24 and <= 26);
        }

        [Fact]
        public void HasChanged_DifferentHash_ReturnsTrue()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot"
            };

            string previousHash = "oldhash";
            bool changed = _service.HasChanged(device, previousHash);
            Assert.True(changed);
        }

        [Fact]
        public void HasChanged_NoHashOnRecord_ReturnsTrue()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot"
            };

            bool changed = _service.HasChanged(device, previousHash: null);
            Assert.True(changed);
        }

        [Fact]
        public void MarkStale_SetsSyncStatusToStale()
        {
            var device = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                ProviderDeviceId = "dev-1",
                Name = "Camera",
                Type = "doorbot",
                SyncStatus = SyncStatus.Synced
            };

            Device marked = _service.MarkStale(device);
            Assert.Equal(SyncStatus.Stale, marked.SyncStatus);
        }
    }
}
