using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingHealthSource implementation.
    /// Verifies device health telemetry fetching and aggregation from Ring devices.
    /// </summary>
    public class RingHealthSourceTests
    {
        [Fact]
        public async Task FetchHealthAsync_WithoutSession_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns((Session?)null);
            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithSessionReturningNullDevices_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync((Devices)null!);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithDoorbots_ExtractsHealthReadings()
        {
            // Arrange
            var health = new DeviceHealth
            {
                Connected = true,
                BatteryPercentage = 75,
                Rssi = -45.3,
                WifiName = "TestWiFi",
                FirmwareVersion = "1.2.3"
            };
            var doorbot = new Doorbot
            {
                Id = 12345,
                Description = "Front Door",
                Health = health
            };

            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("12345", reading.ProviderDeviceId);
            Assert.True(reading.Connected);
            Assert.Equal(75m, reading.BatteryPercentage);
            Assert.Equal(-45, reading.Rssi); // Rounded from -45.3
            Assert.Equal("TestWiFi", reading.WifiName);
            Assert.Equal("1.2.3", reading.FirmwareVersion);
        }

        [Fact]
        public async Task FetchHealthAsync_WithStickupCams_ExtractsHealthReadings()
        {
            // Arrange
            var health = new DeviceHealth
            {
                Connected = false,
                BatteryPercentage = 20,
                Rssi = -55.0,
                WifiName = "GuestWiFi",
                FirmwareVersion = "2.0.0"
            };
            var stickupCam = new StickupCam
            {
                Id = 54321,
                Description = "Backyard",
                Health = health
            };

            var devices = new Devices
            {
                Doorbots = null,
                StickupCams = [stickupCam],
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("54321", reading.ProviderDeviceId);
            Assert.False(reading.Connected);
            Assert.Equal(20m, reading.BatteryPercentage);
            Assert.Equal(-55, reading.Rssi);
            Assert.Equal("GuestWiFi", reading.WifiName);
            Assert.Equal("2.0.0", reading.FirmwareVersion);
        }

        [Fact]
        public async Task FetchHealthAsync_WithAuthorizedDoorbots_ExtractsHealthReadings()
        {
            // Arrange
            var health = new DeviceHealth
            {
                Connected = true,
                BatteryPercentage = 95,
                Rssi = -30.0,
                WifiName = "MainWiFi",
                FirmwareVersion = "1.5.1"
            };
            var authorizedDoorbot = new Doorbot
            {
                Id = 99999,
                Description = "Authorized Front Door",
                Health = health
            };

            var devices = new Devices
            {
                Doorbots = null,
                StickupCams = null,
                AuthorizedDoorbots = [authorizedDoorbot],
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("99999", reading.ProviderDeviceId);
            Assert.True(reading.Connected);
            Assert.Equal(95m, reading.BatteryPercentage);
            Assert.Equal(-30, reading.Rssi);
            Assert.Equal("MainWiFi", reading.WifiName);
            Assert.Equal("1.5.1", reading.FirmwareVersion);
        }

        [Fact]
        public async Task FetchHealthAsync_WithMultipleDeviceTypes_AggregatesAllReadings()
        {
            // Arrange
            var doorbotHealth = new DeviceHealth { Connected = true, BatteryPercentage = 80, Rssi = -40.0, WifiName = "WiFi1", FirmwareVersion = "1.0.0" };
            var doorbot = new Doorbot { Id = 100, Description = "Door 1", Health = doorbotHealth };

            var stickupHealth = new DeviceHealth { Connected = true, BatteryPercentage = 60, Rssi = -50.0, WifiName = "WiFi1", FirmwareVersion = "2.0.0" };
            var stickupCam = new StickupCam { Id = 200, Description = "Cam 1", Health = stickupHealth };

            var authorizedHealth = new DeviceHealth { Connected = false, BatteryPercentage = 10, Rssi = -70.0, WifiName = "WiFi2", FirmwareVersion = "1.5.0" };
            var authorizedDoorbot = new Doorbot { Id = 300, Description = "Door 2", Health = authorizedHealth };

            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = [stickupCam],
                AuthorizedDoorbots = [authorizedDoorbot],
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Equal(3, readings.Count);

            DeviceHealthReading doorReading = readings.First(r => r.ProviderDeviceId == "100");
            Assert.True(doorReading.Connected);
            Assert.Equal(80m, doorReading.BatteryPercentage);

            DeviceHealthReading stickupReading = readings.First(r => r.ProviderDeviceId == "200");
            Assert.True(stickupReading.Connected);
            Assert.Equal(60m, stickupReading.BatteryPercentage);

            DeviceHealthReading authorizedReading = readings.First(r => r.ProviderDeviceId == "300");
            Assert.False(authorizedReading.Connected);
            Assert.Equal(10m, authorizedReading.BatteryPercentage);
        }

        [Fact]
        public async Task FetchHealthAsync_WithNullHealth_SkipsDevice()
        {
            // Arrange - doorbot with null health should be skipped
            var doorbot = new Doorbot
            {
                Id = 111,
                Description = "No Health Data",
                Health = null
            };

            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithStickupCamNullId_SkipsDevice()
        {
            // Arrange - stickup cam with null Id should be skipped
            var stickupCam = new StickupCam
            {
                Id = null,
                DeviceId = "some-device-id",
                Description = "No ID",
                Health = new DeviceHealth { Connected = true, BatteryPercentage = 50 }
            };

            var devices = new Devices
            {
                Doorbots = null,
                StickupCams = [stickupCam],
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithNullBatteryAndRssi_ReturnsNullValues()
        {
            // Arrange - device with null battery and rssi values
            var health = new DeviceHealth
            {
                Connected = true,
                BatteryPercentage = null,
                Rssi = null,
                WifiName = "TestWiFi",
                FirmwareVersion = "1.0.0"
            };
            var doorbot = new Doorbot
            {
                Id = 222,
                Description = "No Battery Data",
                Health = health
            };

            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("222", reading.ProviderDeviceId);
            Assert.True(reading.Connected);
            Assert.Null(reading.BatteryPercentage);
            Assert.Null(reading.Rssi);
            Assert.Equal("TestWiFi", reading.WifiName);
        }

        [Fact]
        public async Task FetchHealthAsync_WithSessionException_ReturnsEmptyList()
        {
            // Arrange - Session.GetRingDevices throws an exception
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ThrowsAsync(new InvalidOperationException("Network error"));
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithRssiRounding_RoundsToNearestInteger()
        {
            // Arrange - verify rssi rounding behavior
            var health = new DeviceHealth
            {
                Connected = true,
                BatteryPercentage = 50,
                Rssi = -45.6, // Should round to -46
                WifiName = "WiFi",
                FirmwareVersion = "1.0"
            };
            var doorbot = new Doorbot { Id = 333, Description = "Rssi Test", Health = health };

            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal(-46, reading.Rssi);
        }

        [Fact]
        public void ConstructorThrowsOnNullLogger()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingHealthSource(null!, sessionProvider.Object));
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingHealthSource(logger, null!));
        }

        [Fact]
        public async Task FetchHealthAsync_WithEmptyDeviceCollections_ReturnsEmptyList()
        {
            // Arrange - all collections are empty arrays
            var devices = new Devices
            {
                Doorbots = [],
                StickupCams = [],
                AuthorizedDoorbots = [],
                Chimes = []
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_CancellationToken_RespectsToken()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            var cts = new CancellationTokenSource();
            cts.Cancel();
            _ = session.Setup(s => s.GetRingDevices())
                .ThrowsAsync(new OperationCanceledException());
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(cts.Token);

            // Assert - exception is caught and returns empty list
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }
    }
}
