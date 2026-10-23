using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests that RingDeviceIdentityResolver stores the device's real kind and online state,
    /// not hardcoded values.
    /// </summary>
    public class RingDeviceIdentityResolverTests
    {
        [Fact]
        public async Task EnsureDeviceIdentityAsync_DoorbellKindOffline_StoresDoorbellTypeAndNotOnline()
        {
            var dataClient = CreateDataClient();
            var resolver = new RingDeviceIdentityResolver(dataClient.Object, new Mock<ILogger>().Object);
            resolver.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await resolver.EnsureDeviceIdentityAsync("123", "Front Door", "loc-1", "doorbell_v4", false, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "123", "Front Door", "doorbell", false,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_ChimeKindOnline_StoresChimeTypeAndOnline()
        {
            var dataClient = CreateDataClient();
            var resolver = new RingDeviceIdentityResolver(dataClient.Object, new Mock<ILogger>().Object);
            resolver.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await resolver.EnsureDeviceIdentityAsync("456", "Hall Chime", "loc-1", "chime_v2", true, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "456", "Hall Chime", "chime", true,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_MissingKind_StoresUnknownType()
        {
            var dataClient = CreateDataClient();
            var resolver = new RingDeviceIdentityResolver(dataClient.Object, new Mock<ILogger>().Object);
            resolver.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await resolver.EnsureDeviceIdentityAsync("789", "Unknown Thing", "loc-1", null, true, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "789", "Unknown Thing", "Unknown", true,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_UnknownOnlineState_StoresNullOnline()
        {
            var dataClient = CreateDataClient();
            var resolver = new RingDeviceIdentityResolver(dataClient.Object, new Mock<ILogger>().Object);
            resolver.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await resolver.EnsureDeviceIdentityAsync("789", "Unknown Thing", "loc-1", "chime_v2", null, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "789", "Unknown Thing", "chime", (bool?)null,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static Mock<IVideoForensicsDataClient> CreateDataClient()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location
                {
                    Id = Guid.NewGuid(),
                    ProviderLocationId = "loc-1",
                    Name = "loc-1"
                });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = "device",
                    Name = "device",
                    Type = "device",
                    IsOnline = false
                });
            return dataClient;
        }
    }
}
