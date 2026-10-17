namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// Inventory discovery in <see cref="EventPullService"/>: locations and devices are not stored when a Ring account
    /// connects, so a pull must discover and persist them from the provider before pulling events.
    /// </summary>
    public class EventPullServiceInventoryTests
    {
        private readonly Mock<IEventAndConfigService> _mockEventAndConfigService = new();
        private readonly Mock<IDeviceRepository> _mockDeviceRepository = new();
        private readonly Mock<ILocationRepository> _mockLocationRepository = new();
        private readonly Mock<IProviderAccountRepository> _mockProviderAccountRepository = new();
        private readonly Mock<IDeviceDiscoveryService> _mockDeviceDiscovery = new();
        private readonly SyncRunOptionsHolder _syncRunOptions = new();

        private EventPullService CreateSut() => new(
            _mockEventAndConfigService.Object,
            _mockDeviceRepository.Object,
            _mockLocationRepository.Object,
            _mockProviderAccountRepository.Object,
            deviceDiscovery: _mockDeviceDiscovery.Object,
            syncRunOptions: _syncRunOptions);

        [Fact]
        public async Task PullAccountEventsAsync_NoStoredInventory_DiscoversAndPersistsLocationsAndDevices()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;
            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken)).ReturnsAsync(account);
            _mockLocationRepository.Setup(r => r.ListByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(Array.Empty<LocationEntity>());
            _mockLocationRepository.Setup(r => r.GetByProviderLocationIdAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync((LocationEntity?)null);
            _mockDeviceRepository.Setup(r => r.GetByProviderDeviceIdAsync(It.IsAny<Guid>(), "dev-1", cancellationToken))
                .ReturnsAsync((DeviceEntity?)null);

            _mockDeviceDiscovery.Setup(d => d.GetLocationsAsync(accountId, cancellationToken))
                .ReturnsAsync(new List<Location> { new("loc-1", "Home", "1 Main St") });
            _mockDeviceDiscovery.Setup(d => d.GetDevicesAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync(new List<Device>
                {
                    new("dev-1", "Front Door", "doorbell", "loc-1", true),
                    new("dev-2", "Kitchen Speaker", "speaker", "loc-1", true)
                });

            // Act
            await CreateSut().PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockLocationRepository.Verify(r => r.AddAsync(
                It.Is<LocationEntity>(l => l.ProviderLocationId == "loc-1" && l.Name == "Home"), cancellationToken), Times.Once);
            _mockLocationRepository.Verify(r => r.LinkToProviderAccountAsync(It.IsAny<Guid>(), accountId, cancellationToken), Times.Once);
            _mockDeviceRepository.Verify(r => r.AddAsync(
                It.Is<DeviceEntity>(d => d.ProviderDeviceId == "dev-1" && d.Name == "Front Door" && d.Type == "doorbell"), cancellationToken), Times.Once);
            _mockDeviceRepository.Verify(r => r.AddAsync(
                It.Is<DeviceEntity>(d => d.ProviderDeviceId == "dev-2" && d.Type == "speaker"), cancellationToken), Times.Once);
        }

        [Fact]
        public async Task EventPullService_NewDevice_RunsDiscoveryAgainOnce()
        {
            // Arrange: the device is not stored yet, so the first discovery pass creates it.
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;
            var account = new ProviderAccountEntity { Id = accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken)).ReturnsAsync(account);
            _mockLocationRepository.Setup(r => r.GetByProviderLocationIdAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync((LocationEntity?)null);
            _mockDeviceRepository.Setup(r => r.GetByProviderDeviceIdAsync(It.IsAny<Guid>(), "dev-1", cancellationToken))
                .ReturnsAsync((DeviceEntity?)null);
            _mockDeviceDiscovery.Setup(d => d.GetLocationsAsync(accountId, cancellationToken))
                .ReturnsAsync(new List<Location> { new("loc-1", "Home", "1 Main St") });
            _mockDeviceDiscovery.Setup(d => d.GetDevicesAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync(new List<Device> { new("dev-1", "Front Door", "doorbell", "loc-1", true) });

            // Act
            await CreateSut().PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

            // Assert: discovery runs a second time so the new device's settings are pulled in the same run.
            _mockDeviceDiscovery.Verify(d => d.GetLocationsAsync(accountId, cancellationToken), Times.Exactly(2));
        }

        [Fact]
        public async Task EventPullService_NewDevice_RepeatPassForcesConfigKind()
        {
            // Arrange: a new device triggers the repeat discovery pass, which must run as a forced config pass.
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;
            var account = new ProviderAccountEntity { Id = accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var originalOptions = new SyncRunOptions(SyncRunKind.Default, SyncRangeKind.Days, Days: 7);
            _syncRunOptions.Current = originalOptions;
            var kindsSeenByDiscovery = new List<SyncRunKind>();

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken)).ReturnsAsync(account);
            _mockLocationRepository.Setup(r => r.GetByProviderLocationIdAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync((LocationEntity?)null);
            _mockDeviceRepository.Setup(r => r.GetByProviderDeviceIdAsync(It.IsAny<Guid>(), "dev-1", cancellationToken))
                .ReturnsAsync((DeviceEntity?)null);
            _mockDeviceDiscovery.Setup(d => d.GetLocationsAsync(accountId, cancellationToken))
                .Callback(() => kindsSeenByDiscovery.Add(_syncRunOptions.Current.Kind))
                .ReturnsAsync(new List<Location> { new("loc-1", "Home", "1 Main St") });
            _mockDeviceDiscovery.Setup(d => d.GetDevicesAsync(accountId, "loc-1", cancellationToken))
                .ReturnsAsync(new List<Device> { new("dev-1", "Front Door", "doorbell", "loc-1", true) });

            // Act
            await CreateSut().PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

            // Assert: the first pass runs under the caller's kind, the repeat runs as config, and the caller's options come back.
            Assert.Equal(new[] { SyncRunKind.Default, SyncRunKind.Config }, kindsSeenByDiscovery);
            Assert.Equal(originalOptions, _syncRunOptions.Current);
        }

        [Fact]
        public async Task EventPullService_NoNewDevice_RunsDiscoveryOnce()
        {
            // Arrange: the device is already stored, so no discovery pass creates one.
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;
            var account = new ProviderAccountEntity { Id = accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            var device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell", IsOnline = true };

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken)).ReturnsAsync(account);
            DiscoveryStub.Wire(accountId, location, new List<DeviceEntity> { device }, _mockDeviceDiscovery, _mockLocationRepository, _mockDeviceRepository);

            // Act
            await CreateSut().PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

            // Assert
            _mockDeviceDiscovery.Verify(d => d.GetLocationsAsync(accountId, cancellationToken), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_DiscoveryFails_RecordsAccountError()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;
            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken)).ReturnsAsync(account);
            _mockDeviceDiscovery.Setup(d => d.GetLocationsAsync(accountId, cancellationToken))
                .ThrowsAsync(new InvalidOperationException("Ring session not restored"));

            // Act
            await CreateSut().PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockProviderAccountRepository.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a => a.LastErrorMessage != null && a.LastErrorMessage.Contains("Ring session not restored")),
                cancellationToken), Times.AtLeastOnce);
            _mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
