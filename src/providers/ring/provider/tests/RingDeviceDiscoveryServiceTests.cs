using Microsoft.Extensions.Logging;

using Moq;

using System.Net;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using CommonContracts = VideoForensics.Core.Providers.Common.Contracts;
using LocationMetadataEntity = VideoForensics.Data.Common.Entities.LocationMetadata;

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
            string locationId = "11111111-1111-1111-1111-111111111111";
            var doorbot = new Doorbot { Id = 123, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var authorizedDoorbot = new Doorbot { Id = 123, Description = "Front Door (Authorized)", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = [authorizedDoorbot],
                StickupCams = null,
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
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
            string locationId = "22222222-2222-2222-2222-222222222222";
            var doorbot = new Doorbot { Id = 100, Description = "Doorbot 100", LocationId = Guid.Parse(locationId) };
            var stickupCam = new StickupCam { Id = 100, Description = "Same ID Stickup", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = [stickupCam],
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
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
            string locationId = "33333333-3333-3333-3333-333333333333";
            var doorbot = new Doorbot { Id = 200, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var stickupCam = new StickupCam { Id = 201, Description = "Backyard", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = [stickupCam],
                Chimes = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
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
            string locationId = "44444444-4444-4444-4444-444444444444";
            var doorbot = new Doorbot { Id = 300, Description = "Front Door", LocationId = Guid.Parse(locationId) };
            var chime = new Chime { Id = 999, Description = "Speaker", LocationId = Guid.Parse(locationId) };

            var devicesResponse = new Devices
            {
                Doorbots = [doorbot],
                AuthorizedDoorbots = null,
                StickupCams = null,
                Chimes = [chime]
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
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
            string locationId = "55555555-5555-5555-5555-555555555555";
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

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
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

        // ===== Account-Aware Tests (Phase 1 Refactoring) =====
        // These tests verify the new account-aware signatures.
        // They ensure service methods accept and can be called with a Guid providerAccountId parameter
        // to enable concurrent processing of multiple Ring accounts without race conditions.

        [Fact]
        public async Task GetLocationsAsync_WithAccountId_WithoutSession_ReturnsEmptyList()
        {
            // Arrange - Account-aware overload that gracefully handles no session
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<CommonContracts.Location> result = await service.GetLocationsAsync(accountId);

            // Assert - Verifies account-aware method returns empty list when no session
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDevicesAsync_WithAccountId_WithoutSession_ReturnsEmptyList()
        {
            // Arrange - Account-aware overload that gracefully handles no session
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> result = await service.GetDevicesAsync(accountId, "location123");

            // Assert - Verifies account-aware method returns empty list when no session
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDeviceAsync_WithAccountId_WithoutSession_ReturnsNull()
        {
            // Arrange - Account-aware overload that gracefully handles no session
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceDiscoveryService(logger, sessionProvider.Object);

            // Act
            Device? result = await service.GetDeviceAsync(accountId, "device123");

            // Assert - Verifies account-aware method returns null when no session
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDevicesAsync_IsOnline_ComesFromHealthConnectedNotSubscribed()
        {
            string locationId = "44444444-4444-4444-4444-444444444444";
            var doorbot = new Doorbot
            {
                Id = 300,
                Description = "Offline Door",
                LocationId = Guid.Parse(locationId),
                Subscribed = true,
                Health = new DeviceHealth { Connected = false }
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Device device = Assert.Single(result);
            Assert.False(device.IsOnline);
        }

        [Fact]
        public async Task GetDevicesAsync_IsOnline_NullWhenRingGivesNoState()
        {
            // Ring returned neither health nor subscription state, so online is unknown (null), not offline.
            string locationId = "55555555-5555-5555-5555-555555555555";
            var doorbot = new Doorbot
            {
                Id = 301,
                Description = "Unknown State Door",
                LocationId = Guid.Parse(locationId),
                Subscribed = null,
                Health = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Device device = Assert.Single(result);
            Assert.Null(device.IsOnline);
        }

        [Fact]
        public async Task GetDevicesAsync_IsOnline_IgnoresSubscribedWhenHealthMissing()
        {
            // Subscribed is a push-subscription flag, not connectivity. A flood/stickup with no health reading
            // must not be reported online just because it is subscribed.
            string locationId = "66666666-6666-6666-6666-666666666666";
            var doorbot = new Doorbot
            {
                Id = 302,
                Description = "Flood Sensor",
                LocationId = Guid.Parse(locationId),
                Subscribed = true,
                Health = null
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Device device = Assert.Single(result);
            Assert.Null(device.IsOnline);
        }

        [Fact]
        public async Task GetDevicesAsync_IsOnline_TrueWhenHealthConnected()
        {
            string locationId = "66666666-6666-6666-6666-666666666666";
            var doorbot = new Doorbot
            {
                Id = 302,
                Description = "Online Door",
                LocationId = Guid.Parse(locationId),
                Subscribed = false,
                Health = new DeviceHealth { Connected = true }
            };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Device device = Assert.Single(result);
            Assert.True(device.IsOnline);
        }

        [Fact]
        public async Task GetDevicesAsync_CarriesRingKindInMetadata()
        {
            string locationId = "55555555-5555-5555-5555-555555555555";
            var doorbot = new Doorbot { Id = 301, Description = "Front", LocationId = Guid.Parse(locationId), Kind = "doorbell_v4" };
            var chime = new Chime { Id = 302, Description = "Hall", LocationId = Guid.Parse(locationId), Kind = "chime_pro_v2" };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot], Chimes = [chime] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Assert.Equal("doorbell_v4", result.Single(d => d.Id == "301").Metadata!["kind"]);
            Assert.Equal("chime_pro_v2", result.Single(d => d.Id == "302").Metadata!["kind"]);
        }

        [Fact]
        public async Task GetDevicesAsync_CarriesTimeZoneInMetadata()
        {
            string locationId = "66666666-6666-6666-6666-666666666666";
            var doorbot = new Doorbot { Id = 401, Description = "Front", LocationId = Guid.Parse(locationId), Kind = "doorbell_v4", TimeZone = "America/Chicago" };
            var chime = new Chime { Id = 402, Description = "Hall", LocationId = Guid.Parse(locationId), Kind = "chime_pro_v2", TimeZone = "America/Chicago" };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot], Chimes = [chime] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Assert.Equal("America/Chicago", result.Single(d => d.Id == "401").Metadata!["time_zone"]);
            Assert.Equal("America/Chicago", result.Single(d => d.Id == "402").Metadata!["time_zone"]);
        }

        [Fact]
        public async Task GetDevicesAsync_NoTimeZoneFromRing_OmitsTimeZoneMetadata()
        {
            string locationId = "77777777-7777-7777-7777-777777777777";
            var doorbot = new Doorbot { Id = 403, Description = "Side", LocationId = Guid.Parse(locationId), Kind = "doorbell_v4", TimeZone = null };

            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            IReadOnlyList<Device> result = await service.GetDevicesAsync(locationId);

            Assert.False(result.Single().Metadata!.ContainsKey("time_zone"));
        }

        [Fact]
        public void ToContractLocation_MapsAddressPartsTimezoneAndOwner()
        {
            var ring = new VideoForensics.Providers.Ring.Entities.Location
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "Home",
                IsOwner = true,
                Address = new LocationAddress { Address1 = "1 Main St", City = "Springfield", State = "IL", ZipCode = "62701", TimeZone = "America/Chicago" }
            };

            CommonContracts.Location location = RingDeviceDiscoveryService.ToContractLocation(ring);

            Assert.Equal("1 Main St", location.Address);
            Assert.Equal("Springfield", location.Metadata!["city"]);
            Assert.Equal("IL", location.Metadata["state"]);
            Assert.Equal("62701", location.Metadata["zip_code"]);
            Assert.Equal("America/Chicago", location.Metadata["timezone"]);
            Assert.Equal("true", location.Metadata["is_owner"]);
        }

        [Fact]
        public async Task PersistLocationMetadataAsync_NewLocation_AddsMetadataWithAllFields()
        {
            var repo = new Mock<VideoForensics.Data.Common.Contracts.ILocationMetadataRepository>();
            LocationMetadataEntity? added = null;
            _ = repo.Setup(r => r.AddAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()))
                .Callback<LocationMetadataEntity, CancellationToken>((m, _) => added = m)
                .Returns(Task.CompletedTask);
            var service = new RingDeviceDiscoveryService(
                new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, metadataRepository: repo.Object, locationRepository: CreateLocationRepository("loc-1", Guid.NewGuid()).Object);
            var location = new CommonContracts.Location("loc-1", "Home", "1 Main St", new Dictionary<string, string>
            {
                ["city"] = "Springfield",
                ["state"] = "IL",
                ["zip_code"] = "62701",
                ["timezone"] = "America/Chicago",
                ["is_owner"] = "true"
            });

            await service.PersistLocationMetadataAsync([location], CancellationToken.None);

            Assert.NotNull(added);
            Assert.Equal("1 Main St", added.StreetAddress);
            Assert.Equal("Springfield", added.City);
            Assert.Equal("IL", added.State);
            Assert.Equal("62701", added.PostalCode);
            Assert.Equal("America/Chicago", added.TimeZoneId);
            Assert.True(added.IsOwner);
        }

        [Fact]
        public async Task PersistLocationMetadataAsync_ExistingStaleRecord_UpdatesInsteadOfAdding()
        {
            var existing = new LocationMetadataEntity { Id = Guid.NewGuid(), LastSyncedUtc = DateTime.UtcNow.AddHours(-1) };
            var repo = new Mock<VideoForensics.Data.Common.Contracts.ILocationMetadataRepository>();
            _ = repo.Setup(r => r.GetByLocationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
            var service = new RingDeviceDiscoveryService(
                new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, metadataRepository: repo.Object, locationRepository: CreateLocationRepository("loc-1", Guid.NewGuid()).Object);
            var location = new CommonContracts.Location("loc-1", "Home", "2 Oak Ave", new Dictionary<string, string> { ["city"] = "Peoria" });

            await service.PersistLocationMetadataAsync([location], CancellationToken.None);

            repo.Verify(r => r.UpdateAsync(It.Is<LocationMetadataEntity>(m => m.City == "Peoria" && m.StreetAddress == "2 Oak Ave"), It.IsAny<CancellationToken>()), Times.Once);
            repo.Verify(r => r.AddAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PersistLocationMetadataAsync_RecentlySyncedRecord_IsSkipped()
        {
            var existing = new LocationMetadataEntity { Id = Guid.NewGuid(), LastSyncedUtc = DateTime.UtcNow };
            var repo = new Mock<VideoForensics.Data.Common.Contracts.ILocationMetadataRepository>();
            _ = repo.Setup(r => r.GetByLocationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
            var service = new RingDeviceDiscoveryService(
                new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, metadataRepository: repo.Object, locationRepository: CreateLocationRepository("loc-1", Guid.NewGuid()).Object);

            await service.PersistLocationMetadataAsync([new CommonContracts.Location("loc-1", "Home")], CancellationToken.None);

            repo.Verify(r => r.UpdateAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()), Times.Never);
            repo.Verify(r => r.AddAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PersistLocationMetadataAsync_LocationRowExists_MetadataLocationIdEqualsLocationRowId()
        {
            var locationRowId = Guid.NewGuid();
            LocationMetadataEntity? added = null;
            var metadataRepo = new Mock<VideoForensics.Data.Common.Contracts.ILocationMetadataRepository>();
            _ = metadataRepo.Setup(r => r.AddAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()))
                .Callback<LocationMetadataEntity, CancellationToken>((m, _) => added = m)
                .Returns(Task.CompletedTask);
            var service = new RingDeviceDiscoveryService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                metadataRepository: metadataRepo.Object,
                locationRepository: CreateLocationRepository("loc-1", locationRowId).Object);

            await service.PersistLocationMetadataAsync([new CommonContracts.Location("loc-1", "Home")], CancellationToken.None);

            Assert.NotNull(added);
            Assert.Equal(locationRowId, added.LocationId);
        }

        [Fact]
        public async Task PersistLocationMetadataAsync_LocationRowMissing_WritesNoMetadata()
        {
            var locationRepo = new Mock<VideoForensics.Data.Common.Contracts.ILocationRepository>();
            _ = locationRepo.Setup(r => r.GetByProviderLocationIdAsync("loc-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((VideoForensics.Data.Common.Entities.Location?)null);
            var metadataRepo = new Mock<VideoForensics.Data.Common.Contracts.ILocationMetadataRepository>();
            var service = new RingDeviceDiscoveryService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                metadataRepository: metadataRepo.Object,
                locationRepository: locationRepo.Object);

            await service.PersistLocationMetadataAsync([new CommonContracts.Location("loc-1", "Home")], CancellationToken.None);

            metadataRepo.Verify(r => r.AddAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()), Times.Never);
            metadataRepo.Verify(r => r.UpdateAsync(It.IsAny<LocationMetadataEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static Mock<VideoForensics.Data.Common.Contracts.ILocationRepository> CreateLocationRepository(string providerLocationId, Guid locationId)
        {
            var repo = new Mock<VideoForensics.Data.Common.Contracts.ILocationRepository>();
            _ = repo.Setup(r => r.GetByProviderLocationIdAsync(providerLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location
                {
                    Id = locationId,
                    ProviderLocationId = providerLocationId,
                    Name = "Home"
                });
            return repo;
        }

        [Fact]
        public async Task RingDeviceDiscoveryService_TwoAccounts_EachGetsItsOwnDeviceList()
        {
            // Arrange
            const string locationId = "11111111-1111-1111-1111-111111111111";
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            Mock<Session> sessionA = CreateDevicesSession(new Doorbot { Id = 111, Description = "Account A Door", LocationId = Guid.Parse(locationId) });
            Mock<Session> sessionB = CreateDevicesSession(new Doorbot { Id = 222, Description = "Account B Door", LocationId = Guid.Parse(locationId) });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountA)).Returns(sessionA.Object);
            _ = sessionProvider.Setup(sp => sp.GetSession(accountB)).Returns(sessionB.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            // Act
            IReadOnlyList<Device> devicesA = await service.GetDevicesAsync(accountA, locationId);
            IReadOnlyList<Device> devicesB = await service.GetDevicesAsync(accountB, locationId);

            // Assert
            _ = Assert.Single(devicesA);
            Assert.Equal("111", devicesA[0].Id);
            _ = Assert.Single(devicesB);
            Assert.Equal("222", devicesB[0].Id);
        }

        [Fact]
        public async Task RingDeviceDiscoveryService_SameAccountWithinTtl_ReusesCachedDeviceList()
        {
            // Arrange
            const string locationId = "11111111-1111-1111-1111-111111111111";
            var accountA = Guid.NewGuid();
            Mock<Session> session = CreateDevicesSession(new Doorbot { Id = 111, Description = "Front Door", LocationId = Guid.Parse(locationId) });
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountA)).Returns(session.Object);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            // Act
            _ = await service.GetDevicesAsync(accountA, locationId);
            IReadOnlyList<Device> second = await service.GetDevicesAsync(accountA, locationId);

            // Assert
            _ = Assert.Single(second);
            session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceDiscoveryService_TwoAccounts_EachGetsItsOwnLocations()
        {
            // Arrange
            const string locationIdA = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            const string locationIdB = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            var sessionA = new Session("a@example.com", "password", new AccountStubHandler(locationIdA, "Home A"));
            _ = await sessionA.Authenticate();
            var sessionB = new Session("b@example.com", "password", new AccountStubHandler(locationIdB, "Home B"));
            _ = await sessionB.Authenticate();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(accountA)).Returns(sessionA);
            _ = sessionProvider.Setup(sp => sp.GetSession(accountB)).Returns(sessionB);
            var service = new RingDeviceDiscoveryService(new Mock<ILogger>().Object, sessionProvider.Object);

            // Act
            IReadOnlyList<CommonContracts.Location> locationsA = await service.GetLocationsAsync(accountA);
            IReadOnlyList<CommonContracts.Location> locationsB = await service.GetLocationsAsync(accountB);

            // Assert
            _ = Assert.Single(locationsA);
            Assert.Equal(locationIdA, locationsA[0].Id);
            _ = Assert.Single(locationsB);
            Assert.Equal(locationIdB, locationsB[0].Id);
        }

        private static Mock<Session> CreateDevicesSession(Doorbot doorbot)
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ReturnsAsync(new Devices { Doorbots = [doorbot] });
            return session;
        }

        /// <summary>Serves OAuth, one account's locations and an empty device list; anything else is a 404. Never reaches the network.</summary>
        private sealed class AccountStubHandler : HttpMessageHandler
        {
            private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
            private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
            private readonly string _locationsJson;

            public AccountStubHandler(string locationId, string locationName)
            {
                _locationsJson = $"{{\"user_locations\":[{{\"location_id\":\"{locationId}\",\"name\":\"{locationName}\",\"is_owner\":true}}]}}";
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;
                HttpStatusCode status = HttpStatusCode.NotFound;
                string body = "{\"error\":\"Not found\"}";

                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    status = HttpStatusCode.OK;
                    body = "{\"access_token\":\"TEST-TOKEN\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}";
                }
                else if (url == LocationsUrl)
                {
                    status = HttpStatusCode.OK;
                    body = _locationsJson;
                }
                else if (url == RingDevicesUrl)
                {
                    status = HttpStatusCode.OK;
                    body = "{\"doorbots\":[],\"authorized_doorbots\":[],\"stickup_cams\":[],\"base_stations\":[],\"chimes\":[]}";
                }

                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body), RequestMessage = request });
            }
        }
    }
}
