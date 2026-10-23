using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Hosting.Tests.Services
{
    public class EventPullServiceTests
    {
        private readonly Mock<IEventAndConfigService> _mockEventAndConfigService;
        private readonly Mock<IDeviceRepository> _mockDeviceRepository;
        private readonly Mock<ILocationRepository> _mockLocationRepository;
        private readonly Mock<IProviderAccountRepository> _mockProviderAccountRepository;
        private readonly Mock<IDeviceDiscoveryService> _mockDiscovery;
        private readonly EventPullService _sut;

        public EventPullServiceTests()
        {
            _mockEventAndConfigService = new Mock<IEventAndConfigService>();
            _mockDeviceRepository = new Mock<IDeviceRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            _mockDiscovery = new Mock<IDeviceDiscoveryService>();

            _sut = new EventPullService(
                _mockEventAndConfigService.Object,
                _mockDeviceRepository.Object,
                _mockLocationRepository.Object,
                _mockProviderAccountRepository.Object,
                _mockDiscovery.Object
            );
        }

        [Fact]
        public async Task PullAccountEventsAsync_FirstConnect_PullsAllData()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            var device = new DeviceEntity
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            var events = new List<DeviceEvent>
            {
                new(
                    Id: "event-1",
                    DeviceId: "device-123",
                    EventType: "motion",
                    Timestamp: DateTime.UtcNow.AddMinutes(-10)
                )
            };

            var config = new DeviceConfig(
                DeviceId: "device-123",
                MotionDetectionEnabled: true,
                MotionSensitivity: 80,
                RecordingMode: "always"
            );

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, _mockDiscovery, _mockLocationRepository, _mockDeviceRepository);

            _ = _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                accountId,
                "device-123",
                DateTime.MinValue,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync(events);

            _ = _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken))
                .ReturnsAsync(config);

            // Act
            _ = await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                accountId,
                "device-123",
                DateTime.MinValue,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);

            _mockEventAndConfigService.Verify(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ExistingAccount_PullsSinceLastAuth()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            DateTime lastAuthTime = DateTime.UtcNow.AddDays(-1);
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow.AddDays(-10),
                LastSuccessfulAuthUtc = lastAuthTime,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            var device = new DeviceEntity
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, _mockDiscovery, _mockLocationRepository, _mockDeviceRepository);

            _ = _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                accountId,
                "device-123",
                lastAuthTime,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync([]);

            _ = _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            _ = await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: lastAuthTime, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockEventAndConfigService.Verify(s => s.GetEventsAsync(
                accountId,
                "device-123",
                lastAuthTime,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            ), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_Success_UpdatesProviderAccountTimestamp()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            var device = new DeviceEntity
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, _mockDiscovery, _mockLocationRepository, _mockDeviceRepository);

            _ = _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync([]);

            _ = _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            _ = await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockProviderAccountRepository.Verify(r => r.UpdateAsync(
                It.Is<ProviderAccountEntity>(a =>
                    a.Id == accountId &&
                    a.LastSuccessfulAuthUtc != null &&
                    a.LastErrorMessage == null
                ),
                cancellationToken
            ), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_Success_UpdatesDeviceTimestamps()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            var device = new DeviceEntity
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, _mockDiscovery, _mockLocationRepository, _mockDeviceRepository);

            _ = _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync([]);

            _ = _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            _ = await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            _mockDeviceRepository.Verify(r => r.UpdateAsync(
                It.Is<DeviceEntity>(d =>
                    d.Id == deviceId &&
                    d.LastSuccessfulPullAtUtc != null &&
                    d.LastPullAttemptAtUtc != null
                ),
                cancellationToken
            ), Times.AtLeastOnce);
        }

        [Fact]
        public async Task PullAccountEventsAsync_Failure_SetsErrorMessage()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            string errorMessage = "Failed to connect to provider";
            CancellationToken cancellationToken = CancellationToken.None;
            VideoForensics.Data.Common.Entities.ProviderAccount? capturedAccount = null;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            var device = new DeviceEntity
            {
                Id = deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            DiscoveryStub.Wire(accountId, location, new[] { device }, _mockDiscovery, _mockLocationRepository, _mockDeviceRepository);

            _ = _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ThrowsAsync(new InvalidOperationException(errorMessage));

            _ = _mockProviderAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<VideoForensics.Data.Common.Entities.ProviderAccount>(), cancellationToken))
                .Callback<VideoForensics.Data.Common.Entities.ProviderAccount, CancellationToken>((acc, ct) => capturedAccount = acc)
                .Returns(Task.CompletedTask);

            // Act
            _ = await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken);

            // Assert
            Assert.NotNull(capturedAccount);
            Assert.Equal(accountId, capturedAccount.Id);
            Assert.NotNull(capturedAccount.LastErrorMessage);
            _ = Assert.NotNull(capturedAccount.LastErrorUtc);
            Assert.True(capturedAccount.LastErrorMessage.Contains(errorMessage));
        }

        [Fact]
        public async Task PullAccountEventsAsync_NoDevices_CompletesSuccessfully()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            var account = new ProviderAccountEntity
            {
                Id = accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var location = new LocationEntity
            {
                Id = locationId,
                ProviderLocationId = "location-123",
                Name = "Home"
            };

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

            _ = _mockLocationRepository.Setup(r => r.ListByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });

            _ = _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(Array.Empty<DeviceEntity>());

            // Act
            Exception? ex = await Record.ExceptionAsync(() => _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken));

            // Assert
            Assert.Null(ex);
            _mockEventAndConfigService.Verify(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, cancellationToken), Times.Never);
        }

        [Fact]
        public async Task PullAccountEventsAsync_AccountNotFound_DoesNotThrow()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            CancellationToken cancellationToken = CancellationToken.None;

            _ = _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync((ProviderAccountEntity?)null);

            // Act
            Exception? ex = await Record.ExceptionAsync(() => _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, toTimestampUtc: null, cancellationToken));

            // Assert
            Assert.Null(ex);
        }
    }
}
