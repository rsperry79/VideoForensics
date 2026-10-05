namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Providers.Common.Contracts;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    public class EventPullServiceTests
    {
        private readonly Mock<IEventAndConfigService> _mockEventAndConfigService;
        private readonly Mock<IDeviceRepository> _mockDeviceRepository;
        private readonly Mock<ILocationRepository> _mockLocationRepository;
        private readonly Mock<IProviderAccountRepository> _mockProviderAccountRepository;
        private readonly EventPullService _sut;

        public EventPullServiceTests()
        {
            _mockEventAndConfigService = new Mock<IEventAndConfigService>();
            _mockDeviceRepository = new Mock<IDeviceRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockProviderAccountRepository = new Mock<IProviderAccountRepository>();

            _sut = new EventPullService(
                _mockEventAndConfigService.Object,
                _mockDeviceRepository.Object,
                _mockLocationRepository.Object,
                _mockProviderAccountRepository.Object
            );
        }

        [Fact]
        public async Task PullAccountEventsAsync_FirstConnect_PullsAllData()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;

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
                new DeviceEvent(
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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(new[] { device });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                accountId,
                "device-123",
                DateTime.MinValue,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync(events);

            _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken))
                .ReturnsAsync(config);

            // Act
            await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

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
            var lastAuthTime = DateTime.UtcNow.AddDays(-1);
            var cancellationToken = CancellationToken.None;

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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(new[] { device });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                accountId,
                "device-123",
                lastAuthTime,
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync(new List<DeviceEvent>());

            _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(accountId, "device-123", cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: lastAuthTime, cancellationToken);

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
            var cancellationToken = CancellationToken.None;

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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(new[] { device });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync(new List<DeviceEvent>());

            _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

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
            var cancellationToken = CancellationToken.None;

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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(new[] { device });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ReturnsAsync(new List<DeviceEvent>());

            _mockEventAndConfigService.Setup(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), cancellationToken))
                .ReturnsAsync(new DeviceConfig("device-123", true, 80, "always"));

            // Act
            await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

            // Assert
            _mockDeviceRepository.Verify(r => r.UpdateAsync(
                It.Is<DeviceEntity>(d =>
                    d.Id == deviceId &&
                    d.LastSuccessfulPullAtUtc != null &&
                    d.LastPullAttemptAtUtc != null
                ),
                cancellationToken
            ), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_Failure_SetsErrorMessage()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var errorMessage = "Failed to connect to provider";
            var cancellationToken = CancellationToken.None;
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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(new[] { device });

            _mockEventAndConfigService.Setup(s => s.GetEventsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                cancellationToken
            )).ThrowsAsync(new InvalidOperationException(errorMessage));

            _mockProviderAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<VideoForensics.Data.Common.Entities.ProviderAccount>(), cancellationToken))
                .Callback<VideoForensics.Data.Common.Entities.ProviderAccount, CancellationToken>((acc, ct) => capturedAccount = acc)
                .Returns(Task.CompletedTask);

            // Act
            await _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken);

            // Assert
            Assert.NotNull(capturedAccount);
            Assert.Equal(accountId, capturedAccount.Id);
            Assert.NotNull(capturedAccount.LastErrorMessage);
            Assert.NotNull(capturedAccount.LastErrorUtc);
            Assert.True(capturedAccount.LastErrorMessage.Contains(errorMessage));
        }

        [Fact]
        public async Task PullAccountEventsAsync_NoDevices_CompletesSuccessfully()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;

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

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync(account);

#pragma warning disable CS0618
            _mockLocationRepository.Setup(r => r.GetByProviderAccountIdAsync(accountId, cancellationToken))
                .ReturnsAsync(new[] { location });
#pragma warning restore CS0618

            _mockDeviceRepository.Setup(r => r.GetByLocationIdAsync(locationId, cancellationToken))
                .ReturnsAsync(Array.Empty<DeviceEntity>());

            // Act
            var ex = await Record.ExceptionAsync(() => _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken));

            // Assert
            Assert.Null(ex);
            _mockEventAndConfigService.Verify(s => s.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, cancellationToken), Times.Never);
        }

        [Fact]
        public async Task PullAccountEventsAsync_AccountNotFound_DoesNotThrow()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var cancellationToken = CancellationToken.None;

            _mockProviderAccountRepository.Setup(r => r.GetAsync(accountId, cancellationToken))
                .ReturnsAsync((ProviderAccountEntity?)null);

            // Act
            var ex = await Record.ExceptionAsync(() => _sut.PullAccountEventsAsync(accountId, fromTimestampUtc: null, cancellationToken));

            // Assert
            Assert.Null(ex);
        }
    }
}
