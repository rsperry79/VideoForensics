using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Records Ring kinds the catalog does not list, from any path that sees a device's kind.</summary>
    public class RingModelDiscoveryTests
    {
        [Fact]
        public async Task RecordUnknownKindAsync_KnownKind_DoesNotRecord()
        {
            Mock<IVideoForensicsDataClient> data = new();

            await RingModelDiscovery.RecordUnknownKindAsync(data.Object, "chime_pro_v2", "Chime", CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RecordUnknownKindAsync_UnknownKind_RecordsUnderRingWithFamily()
        {
            Mock<IVideoForensicsDataClient> data = new();

            await RingModelDiscovery.RecordUnknownKindAsync(data.Object, "doorbell_brand_new", "Doorbell", CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync("Ring", "doorbell_brand_new", "Doorbell", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RecordUnknownKindAsync_BlankKind_DoesNotRecord(string? kind)
        {
            Mock<IVideoForensicsDataClient> data = new();

            await RingModelDiscovery.RecordUnknownKindAsync(data.Object, kind, "Doorbell", CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void FamilyFor_DeviceInDoorbotList_IsDoorbell()
        {
            var devices = new Devices { Doorbots = [new Doorbot { Id = 100 }] };

            Assert.Equal("Doorbell", RingModelDiscovery.FamilyFor(devices, "100"));
        }

        [Fact]
        public void FamilyFor_DeviceInChimeList_IsChime()
        {
            var devices = new Devices { Chimes = [new Chime { Id = 302 }] };

            Assert.Equal("Chime", RingModelDiscovery.FamilyFor(devices, "302"));
        }

        [Fact]
        public void FamilyFor_UnlistedDevice_IsUnknown()
        {
            var devices = new Devices { Doorbots = [new Doorbot { Id = 100 }] };

            Assert.Equal("Unknown", RingModelDiscovery.FamilyFor(devices, "999"));
            Assert.Equal("Unknown", RingModelDiscovery.FamilyFor(null, "100"));
        }
    }
}
