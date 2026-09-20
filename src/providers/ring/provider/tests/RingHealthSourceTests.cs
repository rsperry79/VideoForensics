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

        [Fact]
        public async Task FetchHealthAsync_WithNoProviderAccountRepository_FallsBackToSingleSessionBehavior()
        {
            // Arrange - construct with only 2 args (logger, sessionProvider), no account/auth repos
            // This should behave exactly as the current implementation
            var health = new DeviceHealth { Connected = true, BatteryPercentage = 80, Rssi = -40.0, WifiName = "WiFi1", FirmwareVersion = "1.0.0" };
            var doorbot = new Doorbot { Id = 100, Description = "Door 1", Health = health };
            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert - should have exactly 1 reading from the single session
            Assert.NotNull(readings);
            Assert.Single(readings);
            Assert.Equal("100", readings[0].ProviderDeviceId);
        }

        [Fact]
        public async Task FetchHealthAsync_WithMultipleActiveAccounts_AggregatesReadingsFromAllAccounts()
        {
            // Arrange - construct with all 4 args; provide multiple Ring accounts with different sessions
            var accountId1 = Guid.NewGuid();
            var accountId2 = Guid.NewGuid();

            // Account 1 account object
            var account1 = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId1,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow.AddHours(-1)
            };

            // Account 2 account object
            var account2 = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId2,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow
            };

            // Sessions with different devices
            var health1 = new DeviceHealth { Connected = true, BatteryPercentage = 80, Rssi = -40.0, WifiName = "WiFi1", FirmwareVersion = "1.0.0" };
            var doorbot1 = new Doorbot { Id = 100, Description = "Door 1", Health = health1 };
            var devices1 = new Devices
            {
                Doorbots = [doorbot1],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var health2 = new DeviceHealth { Connected = true, BatteryPercentage = 60, Rssi = -50.0, WifiName = "WiFi2", FirmwareVersion = "2.0.0" };
            var stickupCam2 = new StickupCam { Id = 200, Description = "Cam 1", Health = health2 };
            var devices2 = new Devices
            {
                Doorbots = null,
                StickupCams = [stickupCam2],
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session1 = new Mock<Session>("user1", "pass1", null, null);
            _ = session1.Setup(s => s.GetRingDevices()).ReturnsAsync(devices1);
            var session2 = new Mock<Session>("user2", "pass2", null, null);
            _ = session2.Setup(s => s.GetRingDevices()).ReturnsAsync(devices2);

            // Setup GetSession to return different sessions based on account id
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId1)).Returns(session1.Object);
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId2)).Returns(session2.Object);

            var providerAccountRepository = new Mock<VideoForensics.Data.Common.Contracts.IProviderAccountRepository>();
            _ = providerAccountRepository.Setup(par => par.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.ProviderAccount> { account1, account2 });

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;

            // Create the health source with the optional parameters
            var authServiceMock = new Mock<VideoForensics.Providers.Common.Contracts.IProviderAuthService>();
            var service = new RingHealthSource(logger, sessionProvider.Object, providerAccountRepository.Object, authServiceMock.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert - should have readings from both accounts
            Assert.NotNull(readings);
            Assert.Equal(2, readings.Count);
            Assert.Contains(readings, r => r.ProviderDeviceId == "100");
            Assert.Contains(readings, r => r.ProviderDeviceId == "200");
        }

        [Fact]
        public async Task FetchHealthAsync_WithAccountMissingSession_RestoresViaAuthServiceThenFetches()
        {
            // Arrange - one account, first GetSession returns null, AuthService restores it
            var accountId = Guid.NewGuid();
            var account = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow
            };

            var health = new DeviceHealth { Connected = true, BatteryPercentage = 80, Rssi = -40.0, WifiName = "WiFi1", FirmwareVersion = "1.0.0" };
            var doorbot = new Doorbot { Id = 100, Description = "Door 1", Health = health };
            var devices = new Devices
            {
                Doorbots = [doorbot],
                StickupCams = null,
                AuthorizedDoorbots = null,
                Chimes = null
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("user", "pass", null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(devices);

            // First call returns null, second call (after restore) returns session
            var callCount = 0;
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId))
                .Returns(() =>
                {
                    callCount++;
                    return callCount == 1 ? null : session.Object;
                });

            var providerAccountRepository = new Mock<VideoForensics.Data.Common.Contracts.IProviderAccountRepository>();
            _ = providerAccountRepository.Setup(par => par.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.ProviderAccount> { account });

            var authService = new Mock<VideoForensics.Providers.Common.Contracts.IProviderAuthService>();
            _ = authService.Setup(a => a.RestoreFromSavedCredentialsAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object, providerAccountRepository.Object, authService.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert - should have restored and fetched the session
            Assert.NotNull(readings);
            Assert.Single(readings);
            Assert.Equal("100", readings[0].ProviderDeviceId);
            authService.Verify(ras => ras.RestoreFromSavedCredentialsAsync(accountId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FetchHealthAsync_WithAccountThatCannotBeRestored_SkipsAccountWithoutThrowing()
        {
            // Arrange - one account, GetSession returns null, RestoreFromSavedCredentialsAsync returns false
            var accountId = Guid.NewGuid();
            var account = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow
            };

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns((Session?)null);

            var providerAccountRepository = new Mock<VideoForensics.Data.Common.Contracts.IProviderAccountRepository>();
            _ = providerAccountRepository.Setup(par => par.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.ProviderAccount> { account });

            var authService = new Mock<VideoForensics.Providers.Common.Contracts.IProviderAuthService>();
            _ = authService.Setup(a => a.RestoreFromSavedCredentialsAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Restore failed

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var service = new RingHealthSource(logger, sessionProvider.Object, providerAccountRepository.Object, authService.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert - should return empty list without throwing
            Assert.NotNull(readings);
            Assert.Empty(readings);
        }

        [Fact]
        public async Task FetchHealthAsync_WithOneAccountThrowing_StillReturnsOtherAccountsReadings()
        {
            // Arrange - two accounts, first throws, second succeeds
            var accountId1 = Guid.NewGuid();
            var accountId2 = Guid.NewGuid();

            var account1 = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId1,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow.AddHours(-1)
            };

            var account2 = new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = accountId2,
                ProviderName = "Ring",
                LastSuccessfulAuthUtc = DateTime.UtcNow
            };

            var sessionProvider = new Mock<ISessionProvider>();
            var session1 = new Mock<Session>("user1", "pass1", null, null);
            _ = session1.Setup(s => s.GetRingDevices()).ThrowsAsync(new InvalidOperationException("Network error"));

            var health2 = new DeviceHealth { Connected = true, BatteryPercentage = 60, Rssi = -50.0, WifiName = "WiFi2", FirmwareVersion = "2.0.0" };
            var stickupCam2 = new StickupCam { Id = 200, Description = "Cam 1", Health = health2 };
            var devices2 = new Devices
            {
                Doorbots = null,
                StickupCams = [stickupCam2],
                AuthorizedDoorbots = null,
                Chimes = null
            };
            var session2 = new Mock<Session>("user2", "pass2", null, null);
            _ = session2.Setup(s => s.GetRingDevices()).ReturnsAsync(devices2);

            _ = sessionProvider.Setup(sp => sp.GetSession(accountId1)).Returns(session1.Object);
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId2)).Returns(session2.Object);

            var providerAccountRepository = new Mock<VideoForensics.Data.Common.Contracts.IProviderAccountRepository>();
            _ = providerAccountRepository.Setup(par => par.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.ProviderAccount> { account1, account2 });

            ILogger<RingHealthSource> logger = new Mock<ILogger<RingHealthSource>>().Object;
            var authServiceMock = new Mock<VideoForensics.Providers.Common.Contracts.IProviderAuthService>();
            var service = new RingHealthSource(logger, sessionProvider.Object, providerAccountRepository.Object, authServiceMock.Object);

            // Act
            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            // Assert - should have only the second account's reading, no throw
            Assert.NotNull(readings);
            Assert.Single(readings);
            Assert.Equal("200", readings[0].ProviderDeviceId);
        }
    }
}
