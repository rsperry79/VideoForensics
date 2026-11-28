using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    public class LiveStillCaptureServiceTests
    {
        private static readonly Guid DeviceId = Guid.NewGuid();
        private static readonly Guid LocationId = Guid.NewGuid();
        private static readonly Guid AccountId = Guid.NewGuid();
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IAccountDeviceRepository> _accountDevices = new();
        private readonly Mock<IForensicsConfigurationService> _configuration = new();
        private readonly Mock<IStorageLocationProvider> _storageLocation = new();
        private readonly Mock<ILiveStillStore> _stills = new();

        public LiveStillCaptureServiceTests()
        {
            _ = _devices.Setup(d => d.GetAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeviceDto(DeviceId, LocationId, "123", "Front Door", "doorbot", true, null, null, null,
                    null, SyncStatusDto.Synced));
            _ = _locations.Setup(l => l.GetAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LocationRecordDto(LocationId, "loc-1", "Home", null, null, SyncStatusDto.Synced));
            _ = _accounts.Setup(a => a.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccountDto> { Account(AccountId, "Ring") });
            _ = _accountDevices.Setup(a => a.ListByProviderAccountAsync(AccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { new() { Id = DeviceId, LocationId = LocationId, ProviderDeviceId = "123", Name = "Front Door", Type = "doorbot" } });
            _ = _configuration.Setup(c => c.LoadConfigurationAsync(string.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IForensicsConfiguration>().Object);
            _ = _storageLocation.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>())).Returns(@"F:\Media");
            _ = _stills.Setup(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LiveStillResult.Ok(Guid.NewGuid()));
        }

        private static ProviderAccountDto Account(Guid id, string provider) => new(
            id, Guid.NewGuid(), provider, DateTime.UtcNow, null, true, null, null, null);

        private LiveStillCaptureService CreateService() => new(
            _devices.Object,
            _locations.Object,
            _accounts.Object,
            _accountDevices.Object,
            _configuration.Object,
            _storageLocation.Object,
            _stills.Object);

        [Fact]
        public async Task CaptureAsync_UnknownDevice_ReturnsNotFound_WithoutStoring()
        {
            var unknown = Guid.NewGuid();
            _ = _devices.Setup(d => d.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((DeviceDto?)null);

            LiveStillResult result = await CreateService().CaptureAsync(unknown, Jpeg, "op-1", CancellationToken.None);

            Assert.Equal(LiveStillError.NotFound, result.Error);
            _stills.Verify(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureAsync_NoAccountSeesTheCamera_ReturnsNotFound_WithoutStoring()
        {
            _ = _accountDevices.Setup(a => a.ListByProviderAccountAsync(AccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device>());

            LiveStillResult result = await CreateService().CaptureAsync(DeviceId, Jpeg, "op-1", CancellationToken.None);

            Assert.Equal(LiveStillError.NotFound, result.Error);
            _stills.Verify(s => s.StoreAsync(It.IsAny<LiveStillRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureAsync_Valid_StoresWithMediaRootProviderLocationAndOperator()
        {
            LiveStillResult result = await CreateService().CaptureAsync(DeviceId, Jpeg, "op-1", CancellationToken.None);

            Assert.True(result.Success);
            _stills.Verify(s => s.StoreAsync(
                It.Is<LiveStillRequest>(r =>
                    r.DeviceId == DeviceId
                    && r.MediaRoot == @"F:\Media"
                    && r.ProviderName == "Ring"
                    && r.LocationName == "Home"
                    && r.DeviceName == "Front Door"
                    && r.OperatorName == "op-1"
                    && r.Jpeg.SequenceEqual(Jpeg)),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
