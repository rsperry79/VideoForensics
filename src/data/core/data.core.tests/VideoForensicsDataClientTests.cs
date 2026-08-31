using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class VideoForensicsDataClientTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IProviderAccountRepository> _mockProviderAccountRepository;
        private readonly Mock<IDeviceRepository> _mockDeviceRepository;
        private readonly Mock<IDownloadEventRepository> _mockDownloadEventRepository;
        private readonly Mock<IEventRepository> _mockEventRepository;
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<IDeviceHealthSnapshotRepository> _mockDeviceHealthSnapshotRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IWatermarkService> _mockWatermarkService;
        private readonly Mock<IActionLogger> _mockActionLogger;
        private readonly Mock<ICredentialRepository> _mockCredentialRepository;
        private readonly Mock<IIntegrityVerificationService> _mockIntegrityVerification;
        private readonly Mock<IActionLogRepository> _mockActionLogRepository;
        private readonly Mock<ILogger<VideoForensicsDataClient>> _mockLogger;
        private readonly VideoForensicsDataClient _dataClient;

        private readonly Mock<ILocationRepository> _mockLocationRepository;

        public VideoForensicsDataClientTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockProviderAccountRepository = new Mock<IProviderAccountRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockDeviceRepository = new Mock<IDeviceRepository>();
            _mockDownloadEventRepository = new Mock<IDownloadEventRepository>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockDeviceHealthSnapshotRepository = new Mock<IDeviceHealthSnapshotRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockWatermarkService = new Mock<IWatermarkService>();
            _mockActionLogger = new Mock<IActionLogger>();
            _mockCredentialRepository = new Mock<ICredentialRepository>();
            _mockIntegrityVerification = new Mock<IIntegrityVerificationService>();
            _mockActionLogRepository = new Mock<IActionLogRepository>();
            _mockLogger = new Mock<ILogger<VideoForensicsDataClient>>();

            _dataClient = new VideoForensicsDataClient(
                _mockUserRepository.Object,
                _mockProviderAccountRepository.Object,
                _mockLocationRepository.Object,
                _mockDeviceRepository.Object,
                _mockDownloadEventRepository.Object,
                _mockEventRepository.Object,
                _mockMediaItemRepository.Object,
                _mockDeviceHealthSnapshotRepository.Object,
                _mockUnitOfWork.Object,
                _mockWatermarkService.Object,
                _mockActionLogger.Object,
                _mockCredentialRepository.Object,
                _mockIntegrityVerification.Object,
                _mockActionLogRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task RecordDeviceHealthSnapshotAsync_DelegatesToDeviceHealthSnapshotRepository()
        {
            // Arrange
            var snapshot = new DeviceHealthSnapshot
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                BatteryPercentage = 55m,
                Connected = true,
                CapturedAtUtc = DateTime.UtcNow
            };

            _mockDeviceHealthSnapshotRepository
                .Setup(x => x.AppendSnapshotAsync(snapshot, It.IsAny<CancellationToken>()))
                .ReturnsAsync(snapshot);

            // Act
            var result = await _dataClient.RecordDeviceHealthSnapshotAsync(snapshot, CancellationToken.None);

            // Assert
            Assert.Equal(snapshot, result);
            _mockDeviceHealthSnapshotRepository.Verify(
                x => x.AppendSnapshotAsync(snapshot, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task IsMediaAlreadyDownloadedAsync_DelegatesToDownloadEventRepository()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerEventId = "event-123";

            _mockDownloadEventRepository
                .Setup(x => x.ExistsForProviderEventIdAsync(deviceId, providerEventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _dataClient.IsMediaAlreadyDownloadedAsync(deviceId, providerEventId, CancellationToken.None);

            // Assert
            Assert.True(result);
            _mockDownloadEventRepository.Verify(
                x => x.ExistsForProviderEventIdAsync(deviceId, providerEventId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task IsMediaAlreadyDownloadedAsync_ReturnsFalseWhenNoDownload()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var providerEventId = "event-456";

            _mockDownloadEventRepository
                .Setup(x => x.ExistsForProviderEventIdAsync(deviceId, providerEventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _dataClient.IsMediaAlreadyDownloadedAsync(deviceId, providerEventId, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_InvokesUnitOfWorkWithCorrectOperations()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var downloadEvent = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "event-789",
                AppVersion = "1.0",
                Success = true,
                DownloadStartedUtc = DateTime.UtcNow,
                EventOccurredAtUtc = DateTime.UtcNow
            };

            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                DownloadEventId = downloadEvent.Id,
                FileName = "test.mp4",
                FilePath = "/path/to/test.mp4",
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDownloadEventRepoInContext = new Mock<IDownloadEventRepository>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.DownloadEvents).Returns(mockDownloadEventRepoInContext.Object);
            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "MediaDownloaded",
                EntityType = "DownloadEvent",
                EntityId = downloadEvent.Id,
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    "MediaDownloaded",
                    "DownloadEvent",
                    downloadEvent.Id,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, CancellationToken.None);

            // Assert
            Assert.Equal(downloadEvent, result);
            mockDownloadEventRepoInContext.Verify(
                x => x.AddAsync(downloadEvent, It.IsAny<CancellationToken>()),
                Times.Once);
            mockMediaItemRepoInContext.Verify(
                x => x.AddAsync(mediaItem, It.IsAny<CancellationToken>()),
                Times.Once);
            mockActionLogRepoInContext.Verify(
                x => x.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    "MediaDownloaded",
                    "DownloadEvent",
                    downloadEvent.Id,
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_WithoutMediaItem_SkipsMediaItemAdd()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var downloadEvent = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "event-000",
                AppVersion = "1.0",
                Success = false,
                DownloadStartedUtc = DateTime.UtcNow,
                EventOccurredAtUtc = DateTime.UtcNow
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDownloadEventRepoInContext = new Mock<IDownloadEventRepository>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.DownloadEvents).Returns(mockDownloadEventRepoInContext.Object);
            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "MediaDownloaded",
                EntityType = "DownloadEvent",
                EntityId = downloadEvent.Id,
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.RecordDownloadEventAsync(downloadEvent, null, CancellationToken.None);

            // Assert
            Assert.Equal(downloadEvent, result);
            mockDownloadEventRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<DownloadEvent>(), It.IsAny<CancellationToken>()),
                Times.Once);
            mockMediaItemRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_PropagatesExceptionFromUnitOfWork()
        {
            // Arrange
            var downloadEvent = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                ProviderEventId = "event-err",
                AppVersion = "1.0",
                Success = true,
                DownloadStartedUtc = DateTime.UtcNow,
                EventOccurredAtUtc = DateTime.UtcNow
            };

            var testException = new InvalidOperationException("Unit of work failed");

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _dataClient.RecordDownloadEventAsync(downloadEvent, null, CancellationToken.None));

            Assert.Equal("Unit of work failed", exception.Message);
        }

        [Fact]
        public async Task EnsureUserAndAccountAsync_CreatesNewUserWhenNoneExists()
        {
            // Arrange
            var providerName = "Ring";
            var providerUserKey = "user-key-123";
            var displayName = "John Doe";
            var email = "john@example.com";

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockUserRepoInContext = new Mock<IUserRepository>();
            var mockProviderAccountRepoInContext = new Mock<IProviderAccountRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.Users).Returns(mockUserRepoInContext.Object);
            mockContext.Setup(x => x.ProviderAccounts).Returns(mockProviderAccountRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            // Setup Users repository to return empty list
            mockUserRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Setup ProviderAccounts repository to return empty list
            mockProviderAccountRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccount>());

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "UserCreated",
                EntityType = "ProviderAccount",
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var (user, account) = await _dataClient.EnsureUserAndAccountAsync(
                providerName, providerUserKey, displayName, email, CancellationToken.None);

            // Assert
            Assert.NotNull(user);
            Assert.Equal(providerUserKey, user.ProviderUserKey);
            Assert.Equal(displayName, user.DisplayName);
            Assert.Equal(email, user.Email);

            Assert.NotNull(account);
            Assert.Equal(providerName, account.ProviderName);
            Assert.Equal(user.Id, account.UserId);
            Assert.True(account.IsActive);

            mockUserRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                Times.Once);
            mockProviderAccountRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureUserAndAccountAsync_ReusesExistingUserAndAccount()
        {
            // Arrange
            var providerName = "Ring";
            var providerUserKey = "user-key-456";
            var displayName = "Jane Smith";
            var email = "jane@example.com";

            var existingUser = new User
            {
                Id = Guid.NewGuid(),
                ProviderUserKey = providerUserKey,
                DisplayName = displayName,
                Email = email,
                CreatedUtc = DateTime.UtcNow
            };

            var existingAccount = new ProviderAccount
            {
                Id = Guid.NewGuid(),
                UserId = existingUser.Id,
                ProviderName = providerName,
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockUserRepoInContext = new Mock<IUserRepository>();
            var mockProviderAccountRepoInContext = new Mock<IProviderAccountRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.Users).Returns(mockUserRepoInContext.Object);
            mockContext.Setup(x => x.ProviderAccounts).Returns(mockProviderAccountRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            mockUserRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { existingUser });

            mockProviderAccountRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccount> { existingAccount });

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "AccountLinked",
                EntityType = "ProviderAccount",
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"};

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var (user, account) = await _dataClient.EnsureUserAndAccountAsync(
                providerName, providerUserKey, displayName, email, CancellationToken.None);

            // Assert
            Assert.Equal(existingUser.Id, user.Id);
            Assert.Equal(existingAccount.Id, account.Id);

            mockUserRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
                Times.Never);
            mockProviderAccountRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetWatermarkAsync_DelegatesToWatermarkService()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var requestedDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var expectedDate = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc);

            _mockWatermarkService
                .Setup(x => x.ResolveStartDateAsync(deviceId, requestedDate, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDate);

            // Act
            var result = await _dataClient.GetWatermarkAsync(deviceId, requestedDate, true, CancellationToken.None);

            // Assert
            Assert.Equal(expectedDate, result);
            _mockWatermarkService.Verify(
                x => x.ResolveStartDateAsync(deviceId, requestedDate, true, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Credentials_ReturnsCredentialRepository()
        {
            // Act
            var result = _dataClient.Credentials;

            // Assert
            Assert.Same(_mockCredentialRepository.Object, result);
        }

        [Fact]
        public void IntegrityVerification_ReturnsIntegrityVerificationService()
        {
            // Act
            var result = _dataClient.IntegrityVerification;

            // Assert
            Assert.Same(_mockIntegrityVerification.Object, result);
        }

        [Fact]
        public void ActionLog_ReturnsActionLogRepository()
        {
            // Act
            var result = _dataClient.ActionLog;

            // Assert
            Assert.Same(_mockActionLogRepository.Object, result);
        }

        [Fact]
        public async Task EnsureLocationAsync_CreatesNewLocationWhenNoneExists()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            var providerLocationId = "location-123";
            var locationName = "Front Door";
            var address = "123 Main St";

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();

            mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);

            // Setup Locations repository to return empty list
            mockLocationRepoInContext
                .Setup(x => x.GetByProviderAccountIdAsync(providerAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Location>());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, locationName, address, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(providerLocationId, result.ProviderLocationId);
            Assert.Equal(locationName, result.Name);
            Assert.Equal(address, result.Address);
            Assert.Equal(providerAccountId, result.ProviderAccountId);

            mockLocationRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureLocationAsync_ReusesExistingLocation()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            var providerLocationId = "location-456";
            var locationName = "Back Door";
            var address = "123 Main St, Back";

            var existingLocation = new Location
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = providerAccountId,
                ProviderLocationId = providerLocationId,
                Name = locationName,
                Address = address
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();

            mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);

            mockLocationRepoInContext
                .Setup(x => x.GetByProviderAccountIdAsync(providerAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Location> { existingLocation });

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, locationName, address, ct: CancellationToken.None);

            // Assert
            Assert.Equal(existingLocation.Id, result.Id);

            mockLocationRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EnsureDeviceAsync_CreatesNewDeviceWhenNoneExists()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            var providerDeviceId = "device-123";
            var deviceName = "Front Camera";
            var deviceType = "camera";
            var isOnline = true;

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            // Setup Devices repository to return empty list for both the location-scoped lookup and
            // the account-wide fallback lookup (this device genuinely doesn't exist anywhere yet).
            mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device>());
            mockDeviceRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device>());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.EnsureDeviceAsync(
                locationId, providerDeviceId, deviceName, deviceType, isOnline, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(providerDeviceId, result.ProviderDeviceId);
            Assert.Equal(deviceName, result.Name);
            Assert.Equal(deviceType, result.Type);
            Assert.True(result.IsOnline);
            Assert.Equal(locationId, result.LocationId);

            mockDeviceRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceAsync_RelocatesDeviceFoundUnderDifferentLocation()
        {
            // Arrange: device was previously recorded under a placeholder/synthetic location (as
            // happened before locations were resolved per-device); it must be relocated to the real
            // location rather than creating a duplicate row with the same ProviderDeviceId.
            var placeholderLocationId = Guid.NewGuid();
            var realLocationId = Guid.NewGuid();
            var providerDeviceId = "device-789";

            var existingDevice = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = placeholderLocationId,
                ProviderDeviceId = providerDeviceId,
                Name = "Front Camera",
                Type = "camera",
                IsOnline = true
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            // Not found under the real location (that's the whole point)...
            mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(realLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device>());
            // ...but found via the account-wide fallback lookup, still under the old location.
            mockDeviceRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { existingDevice });

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.EnsureDeviceAsync(
                realLocationId, providerDeviceId, "Front Camera", "camera", true, ct: CancellationToken.None);

            // Assert: same device Id (no duplicate created), now pointing at the real location.
            Assert.Equal(existingDevice.Id, result.Id);
            Assert.Equal(realLocationId, result.LocationId);

            mockDeviceRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Never);
            mockDeviceRepoInContext.Verify(
                x => x.UpdateAsync(It.Is<Device>(d => d.LocationId == realLocationId), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceAsync_UpdatesExistingDevice()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            var providerDeviceId = "device-456";
            var newDeviceName = "Front Camera Updated";
            var newDeviceType = "doorbell";
            var isOnline = false;

            var existingDevice = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ProviderDeviceId = providerDeviceId,
                Name = "Front Camera",
                Type = "camera",
                IsOnline = true
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Device> { existingDevice });

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await _dataClient.EnsureDeviceAsync(
                locationId, providerDeviceId, newDeviceName, newDeviceType, isOnline, ct: CancellationToken.None);

            // Assert
            Assert.Equal(existingDevice.Id, result.Id);
            Assert.Equal(newDeviceName, result.Name);
            Assert.Equal(newDeviceType, result.Type);
            Assert.False(result.IsOnline);

            mockDeviceRepoInContext.Verify(
                x => x.UpdateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Once);
            mockDeviceRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateDeviceWatermarkAsync_WithNewerTimestamp_AdvancesWatermark()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var currentWatermark = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var newerTimestamp = currentWatermark.AddHours(1);
            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "provider-1",
                Name = "Front Camera",
                Type = "camera",
                LastSuccessfulPullAtUtc = currentWatermark
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            mockDeviceRepoInContext.Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) => await work(mockContext.Object));

            // Act
            await _dataClient.UpdateDeviceWatermarkAsync(deviceId, newerTimestamp, CancellationToken.None);

            // Assert
            Assert.Equal(newerTimestamp, device.LastSuccessfulPullAtUtc);
            mockDeviceRepoInContext.Verify(x => x.UpdateAsync(device, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDeviceWatermarkAsync_WithOlderTimestamp_DoesNotRegressWatermark()
        {
            // Arrange: simulates a batch where events download concurrently and complete out of
            // timestamp order - an earlier event finishing last must not undo a later watermark.
            var deviceId = Guid.NewGuid();
            var currentWatermark = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var olderTimestamp = currentWatermark.AddHours(-1);
            var device = new Device
            {
                Id = deviceId,
                ProviderDeviceId = "provider-1",
                Name = "Front Camera",
                Type = "camera",
                LastSuccessfulPullAtUtc = currentWatermark
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            mockDeviceRepoInContext.Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) => await work(mockContext.Object));

            // Act
            await _dataClient.UpdateDeviceWatermarkAsync(deviceId, olderTimestamp, CancellationToken.None);

            // Assert
            Assert.Equal(currentWatermark, device.LastSuccessfulPullAtUtc);
            mockDeviceRepoInContext.Verify(x => x.UpdateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
