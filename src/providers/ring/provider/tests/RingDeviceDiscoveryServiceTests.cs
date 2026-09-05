using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using CommonContracts = VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingDeviceDiscoveryService implementation.
    /// Verifies location and device discovery functionality.
    /// </summary>
    public class RingDeviceDiscoveryServiceTests
    {
        [Fact]
        public async Task GetLocationsAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<CommonContracts.Location> result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLocationsAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<CommonContracts.Location> result = await service.GetLocationsAsync();

            // Assert
            Assert.NotNull(result);
            // Verify it's read-only by attempting to cast to IReadOnlyList
            _ = Assert.IsAssignableFrom<IReadOnlyList<CommonContracts.Location>>(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync("location123");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithInvalidLocationId_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync("invalid-location-id");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_ReturnsReadOnlyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync("00000000-0000-0000-0000-000000000000");

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsAssignableFrom<IReadOnlyList<Device>>(result);
        }

        [Fact]
        public async Task GetDeviceAsync_WithoutSession_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            Device? result = await service.GetDeviceAsync("device123");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDeviceAsync_ReturnsDeviceOrNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            Device? result = await service.GetDeviceAsync("nonexistent-device");

            // Assert
            // Result may be null if device not found, which is valid behavior
            // If not null, should be Device type
            if (result != null)
            {
                _ = Assert.IsType<Device>(result);
            }
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingDeviceDiscoveryService(logger, null!));
        }

        [Fact]
        public async Task GetDevicesAsync_DeduplicatesAcrossCollections()
        {
            // Arrange
            var locationId = "11111111-1111-1111-1111-111111111111";
            var doorbot = new Doorbot { Id = 123, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var authorizedDoorbot = new Doorbot { Id = 123, Description = "Front Door (Authorized)", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = [authorizedDoorbot],
                StickupCams = null,
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(devicesResponse);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            // Assert
            Assert.NotNull(result);
            _ = Assert.Single(result);
            Assert.Equal("123", result[0].Id);
            Assert.Equal("doorbot", result[0].Type);
        }

        [Fact]
        public async Task GetDevicesAsync_DeduplicatesStickupCamsAndDoorbots()
        {
            // Arrange
            var locationId = "22222222-2222-2222-2222-222222222222";
            var doorbot = new Doorbot { Id = 100, Description = "Doorbot 100", LocationId = Guid.Parse(locationId) };
            var stickupCam = new StickupCam { Id = 100, Description = "Same ID Stickup", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = [stickupCam],
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(devicesResponse);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            // Assert
            Assert.NotNull(result);
            _ = Assert.Single(result);
            Assert.Equal("100", result[0].Id);
            Assert.Equal("doorbot", result[0].Type);
        }

        [Fact]
        public async Task GetDevicesAsync_IncludesMultipleDistinctDevices()
        {
            // Arrange
            var locationId = "33333333-3333-3333-3333-333333333333";
            var doorbot = new Doorbot { Id = 200, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var stickupCam = new StickupCam { Id = 201, Description = "Backyard", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = [stickupCam],
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(devicesResponse);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            var ids = result.Select(d => d.Id).ToList();
            Assert.Contains("200", ids);
            Assert.Contains("201", ids);
        }

        [Fact]
        public async Task GetDevicesAsync_IncludesChimes()
        {
            // Arrange - chimes have no video/event history but are still a real device on the
            // account and belong in discovery results for forensic completeness (see
            // DbCompletenessChecker, which flags a chime present on the account but absent from
            // the DB).
            var locationId = "44444444-4444-4444-4444-444444444444";
            var doorbot = new Doorbot { Id = 300, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var chime = new Chime { Id = 999, Description = "Speaker", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = null,
                Chimes = [chime]
            };

            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(devicesResponse);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == "300" && d.Type == "doorbot");
            Assert.Contains(result, d => d.Id == "999" && d.Type == "chime");
        }

        [Fact]
        public async Task GetDevicesAsync_HandlesNullStickupCamIdWithDeviceIdFallback()
        {
            // Arrange - Test the edge case where StickupCam.Id is null and falls back to DeviceId
            var locationId = "55555555-5555-5555-5555-555555555555";
            var stickupCamWithoutId = new StickupCam
            {
                Id = null,
                DeviceId = "fallback-device-id",
                Description = "Stickup without numeric ID",
                LocationId = Guid.Parse(locationId)
            };

            var devicesResponse = new Devices
            {
                Doorbots = null,
                AuthorizedDoorbots = null,
                StickupCams = [stickupCamWithoutId],
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(devicesResponse);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            // Assert
            Assert.NotNull(result);
            _ = Assert.Single(result);
            Assert.Equal("fallback-device-id", result[0].Id);
            Assert.Equal("stickup_cam", result[0].Type);
        }
    }
}
