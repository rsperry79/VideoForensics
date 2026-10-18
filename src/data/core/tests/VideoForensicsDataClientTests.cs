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
        private readonly Mock<IDeviceHealthRepository> _mockDeviceHealthRepository;
        private readonly Mock<IDeviceDetailsRepository> _mockDeviceDetailsRepository;
        private readonly Mock<IProviderApiErrorLogRepository> _mockProviderApiErrorLogRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IWatermarkService> _mockWatermarkService;
        private readonly Mock<IActionLogger> _mockActionLogger;
        private readonly Mock<ICredentialRepository> _mockCredentialRepository;
        private readonly Mock<IIntegrityVerificationService> _mockIntegrityVerification;
        private readonly Mock<IActionLogRepository> _mockActionLogRepository;
        private readonly Mock<ILogger<VideoForensicsDataClient>> _mockLogger;
        private readonly Mock<IMediaAnalysisService> _mockMediaAnalysis;
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
            _mockDeviceHealthRepository = new Mock<IDeviceHealthRepository>();
            _mockDeviceDetailsRepository = new Mock<IDeviceDetailsRepository>();
            _mockProviderApiErrorLogRepository = new Mock<IProviderApiErrorLogRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockWatermarkService = new Mock<IWatermarkService>();
            _mockActionLogger = new Mock<IActionLogger>();
            _mockCredentialRepository = new Mock<ICredentialRepository>();
            _mockIntegrityVerification = new Mock<IIntegrityVerificationService>();
            _mockActionLogRepository = new Mock<IActionLogRepository>();
            _mockLogger = new Mock<ILogger<VideoForensicsDataClient>>();
            _mockMediaAnalysis = new Mock<IMediaAnalysisService>();

            _dataClient = new VideoForensicsDataClient(
                _mockUserRepository.Object,
                _mockProviderAccountRepository.Object,
                _mockLocationRepository.Object,
                _mockDeviceRepository.Object,
                _mockDownloadEventRepository.Object,
                _mockEventRepository.Object,
                _mockMediaItemRepository.Object,
                _mockDeviceHealthRepository.Object,
                _mockDeviceDetailsRepository.Object,
                _mockProviderApiErrorLogRepository.Object,
                _mockUnitOfWork.Object,
                _mockWatermarkService.Object,
                _mockActionLogger.Object,
                _mockCredentialRepository.Object,
                _mockIntegrityVerification.Object,
                _mockActionLogRepository.Object,
                _mockLogger.Object,
                _mockMediaAnalysis.Object);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_WithMedia_RunsAnalysisAfterSaveAndWaits()
        {
            var media = new MediaItem { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = "c.mp4", FilePath = "c.mp4", MediaFormat = "mp4", Sha256Hash = "h" };
            var evt = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = media.DeviceId, ProviderEventId = "e1", AppVersion = "test" };
            var context = new Mock<IUnitOfWorkContext> { DefaultValue = DefaultValue.Mock };
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) => await work(context.Object));

            _ = await _dataClient.RecordDownloadEventAsync(evt, media, CancellationToken.None);

            _mockMediaAnalysis.Verify(a => a.AnalyzeAsync(media.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_AnalysisFails_RecordingStillSucceeds()
        {
            var media = new MediaItem { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = "c.mp4", FilePath = "c.mp4", MediaFormat = "mp4", Sha256Hash = "h" };
            var evt = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = media.DeviceId, ProviderEventId = "e2", AppVersion = "test" };
            var context = new Mock<IUnitOfWorkContext> { DefaultValue = DefaultValue.Mock };
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) => await work(context.Object));
            _mockMediaAnalysis.Setup(a => a.AnalyzeAsync(media.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("ffmpeg missing"));

            DownloadEvent result = await _dataClient.RecordDownloadEventAsync(evt, media, CancellationToken.None);

            Assert.Same(evt, result);
        }

        [Fact]
        public async Task RecordDownloadEventAsync_Cancelled_DuringAnalysis_PropagatesCancellation()
        {
            using var cts = new CancellationTokenSource();
            var media = new MediaItem { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FileName = "c.mp4", FilePath = "c.mp4", MediaFormat = "mp4", Sha256Hash = "h" };
            var evt = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = media.DeviceId, ProviderEventId = "e3", AppVersion = "test" };
            var context = new Mock<IUnitOfWorkContext> { DefaultValue = DefaultValue.Mock };
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) => await work(context.Object));
            _mockMediaAnalysis
                .Setup(a => a.AnalyzeAsync(media.Id, It.IsAny<CancellationToken>()))
                .Returns<Guid, CancellationToken>((_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; });

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _dataClient.RecordDownloadEventAsync(evt, media, cts.Token));
        }

        [Fact]
        public async Task IsMediaAlreadyDownloadedAsync_DelegatesToDownloadEventRepository()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            string providerEventId = "event-123";

            _ = _mockDownloadEventRepository
                .Setup(x => x.ExistsForProviderEventIdAsync(deviceId, providerEventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            bool result = await _dataClient.IsMediaAlreadyDownloadedAsync(deviceId, providerEventId, CancellationToken.None);

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
            string providerEventId = "event-456";

            _ = _mockDownloadEventRepository
                .Setup(x => x.ExistsForProviderEventIdAsync(deviceId, providerEventId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            bool result = await _dataClient.IsMediaAlreadyDownloadedAsync(deviceId, providerEventId, CancellationToken.None);

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

            _ = mockContext.Setup(x => x.DownloadEvents).Returns(mockDownloadEventRepoInContext.Object);
            _ = mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            _ = mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = deviceId, LocationId = Guid.NewGuid(), ProviderDeviceId = "device-1", Name = "Front Door", Type = "doorbell" });

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
                EntryHash = "test_hash"
            };

            _ = mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    "MediaDownloaded",
                    "DownloadEvent",
                    downloadEvent.Id,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            DownloadEvent result = await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, CancellationToken.None);

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

            _ = mockContext.Setup(x => x.DownloadEvents).Returns(mockDownloadEventRepoInContext.Object);
            _ = mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            _ = mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = deviceId, LocationId = Guid.NewGuid(), ProviderDeviceId = "device-1", Name = "Front Door", Type = "doorbell" });

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
                EntryHash = "test_hash"
            };

            _ = mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            DownloadEvent result = await _dataClient.RecordDownloadEventAsync(downloadEvent, null, CancellationToken.None);

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
        public async Task RecordDownloadEventAsync_LogsDeviceName_NotDeviceId()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var downloadEvent = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = "event-name",
                AppVersion = "1.0",
                Success = true,
                DownloadStartedUtc = DateTime.UtcNow,
                EventOccurredAtUtc = DateTime.UtcNow
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDownloadEventRepoInContext = new Mock<IDownloadEventRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            _ = mockContext.Setup(x => x.DownloadEvents).Returns(mockDownloadEventRepoInContext.Object);
            _ = mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = deviceId, LocationId = Guid.NewGuid(), ProviderDeviceId = "device-1", Name = "Front Door", Type = "doorbell" });
            _ = mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(It.IsAny<string>(), It.IsAny<ActorType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActionLogEntry { Id = Guid.NewGuid(), Actor = Environment.UserName, ActorType = ActorType.Human, Action = "MediaDownloaded", EntityType = "DownloadEvent", EntityId = downloadEvent.Id, TimestampUtc = DateTime.UtcNow, EntryHash = "test_hash" });

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<DownloadEvent>> work, CancellationToken ct) => await work(mockContext.Object));

            // Act
            _ = await _dataClient.RecordDownloadEventAsync(downloadEvent, null, CancellationToken.None);

            // Assert: the info log must name the device, not expose its GUID
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Front Door") && !v.ToString()!.Contains(deviceId.ToString())),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<DownloadEvent>>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _dataClient.RecordDownloadEventAsync(downloadEvent, null, CancellationToken.None));

            Assert.Equal("Unit of work failed", exception.Message);
        }

        [Fact]
        public async Task EnsureUserAndAccountAsync_CreatesNewUserWhenNoneExists()
        {
            // Arrange
            string providerName = "Ring";
            string providerUserKey = "user-key-123";
            string displayName = "John Doe";
            string email = "john@example.com";

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockUserRepoInContext = new Mock<IUserRepository>();
            var mockProviderAccountRepoInContext = new Mock<IProviderAccountRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            _ = mockContext.Setup(x => x.Users).Returns(mockUserRepoInContext.Object);
            _ = mockContext.Setup(x => x.ProviderAccounts).Returns(mockProviderAccountRepoInContext.Object);
            _ = mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            // Setup Users repository to return empty list
            _ = mockUserRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            // Setup ProviderAccounts repository to return empty list
            _ = mockProviderAccountRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "UserCreated",
                EntityType = "ProviderAccount",
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"
            };

            _ = mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            (User? user, ProviderAccount? account) = await _dataClient.EnsureUserAndAccountAsync(
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
            string providerName = "Ring";
            string providerUserKey = "user-key-456";
            string displayName = "Jane Smith";
            string email = "jane@example.com";

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

            _ = mockContext.Setup(x => x.Users).Returns(mockUserRepoInContext.Object);
            _ = mockContext.Setup(x => x.ProviderAccounts).Returns(mockProviderAccountRepoInContext.Object);
            _ = mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            _ = mockUserRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingUser]);

            _ = mockProviderAccountRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingAccount]);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "AccountLinked",
                EntityType = "ProviderAccount",
                TimestampUtc = DateTime.UtcNow
            ,
                EntryHash = "test_hash"
            };

            _ = mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLogEntry);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<(User, ProviderAccount)>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            (User? user, ProviderAccount? account) = await _dataClient.EnsureUserAndAccountAsync(
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

            _ = _mockWatermarkService
                .Setup(x => x.ResolveStartDateAsync(deviceId, requestedDate, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDate);

            // Act
            DateTime result = await _dataClient.GetWatermarkAsync(deviceId, requestedDate, true, CancellationToken.None);

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
            ICredentialRepository result = _dataClient.Credentials;

            // Assert
            Assert.Same(_mockCredentialRepository.Object, result);
        }

        [Fact]
        public void IntegrityVerification_ReturnsIntegrityVerificationService()
        {
            // Act
            IIntegrityVerificationService result = _dataClient.IntegrityVerification;

            // Assert
            Assert.Same(_mockIntegrityVerification.Object, result);
        }

        [Fact]
        public void ActionLog_ReturnsActionLogRepository()
        {
            // Act
            IActionLogRepository result = _dataClient.ActionLog;

            // Assert
            Assert.Same(_mockActionLogRepository.Object, result);
        }

        [Fact]
        public async Task EnsureLocationAsync_CreatesNewLocationWhenNoneExists()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            string providerLocationId = "location-123";
            string locationName = "Front Door";
            string address = "123 Main St";

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();

            _ = mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);

            // Setup Locations repository to return null (no existing location)
            _ = mockLocationRepoInContext
                .Setup(x => x.GetByProviderLocationIdAsync(providerLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Location?)null);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Location result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, locationName, address, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(providerLocationId, result.ProviderLocationId);
            Assert.Equal(locationName, result.Name);
            Assert.Equal(address, result.Address);

            mockLocationRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureLocationAsync_ReusesExistingLocation()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            string providerLocationId = "location-456";
            string locationName = "Back Door";
            string address = "123 Main St, Back";

            var existingLocation = new Location
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                Name = locationName,
                Address = address
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();

            _ = mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);

            _ = mockLocationRepoInContext
                .Setup(x => x.GetByProviderLocationIdAsync(providerLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingLocation);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Location result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, locationName, address, ct: CancellationToken.None);

            // Assert
            Assert.Equal(existingLocation.Id, result.Id);

            mockLocationRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task VideoForensicsDataClient_EnsureLocation_NewLocation_LinksToProviderAccount()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            string providerLocationId = "location-link-new";

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();
            _ = mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);
            _ = mockLocationRepoInContext
                .Setup(x => x.GetByProviderLocationIdAsync(providerLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Location?)null);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Location result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, "Name", "Addr", ct: CancellationToken.None);

            // Assert
            mockLocationRepoInContext.Verify(
                x => x.LinkToProviderAccountAsync(result.Id, providerAccountId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task VideoForensicsDataClient_EnsureLocation_ExistingLocation_StillLinksToProviderAccount()
        {
            // Arrange
            var providerAccountId = Guid.NewGuid();
            string providerLocationId = "location-link-existing";
            var existingLocation = new Location
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                Name = "Existing"
            };

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockLocationRepoInContext = new Mock<ILocationRepository>();
            _ = mockContext.Setup(x => x.Locations).Returns(mockLocationRepoInContext.Object);
            _ = mockLocationRepoInContext
                .Setup(x => x.GetByProviderLocationIdAsync(providerLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingLocation);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Location>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Location>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Location result = await _dataClient.EnsureLocationAsync(
                providerAccountId, providerLocationId, "Existing", null, ct: CancellationToken.None);

            // Assert
            Assert.Equal(existingLocation.Id, result.Id);
            mockLocationRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()),
                Times.Never);
            mockLocationRepoInContext.Verify(
                x => x.LinkToProviderAccountAsync(existingLocation.Id, providerAccountId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceAsync_CreatesNewDeviceWhenNoneExists()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            string providerDeviceId = "device-123";
            string deviceName = "Front Camera";
            string deviceType = "camera";
            bool isOnline = true;

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            // Setup Devices repository to return empty list for both the location-scoped lookup and
            // the account-wide fallback lookup (this device genuinely doesn't exist anywhere yet).
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = mockDeviceRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
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
        public async Task EnsureDeviceAsync_UnknownOnlineState_StoresNull()
        {
            // Arrange: Ring gave no connection/subscription state, so the online flag is unknown.
            var locationId = Guid.NewGuid();
            string providerDeviceId = "device-unknown-state";
            bool? isOnline = null;

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();

            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = mockDeviceRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, providerDeviceId, "Unknown Cam", "camera", isOnline, ct: CancellationToken.None);

            // Assert: unknown is stored as null, not false.
            Assert.Null(result.IsOnline);
            mockDeviceRepoInContext.Verify(
                x => x.AddAsync(It.Is<Device>(d => d.IsOnline == null), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceAsync_WithDetailFields_StoresThemOnCreate()
        {
            var locationId = Guid.NewGuid();
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = mockDeviceRepoInContext.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));
            var details = new VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields(
                RingDeviceId: "aabbcc", RingKind: "doorbell_v4", FirmwareVersion: "2.0.1",
                Latitude: 41.5, Longitude: -87.6, Address: "1 Main St", Subscribed: true, SubscribedMotions: false,
                BatteryLife: "87", ExternalConnection: true, Owned: true, Stolen: false);

            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-detail", "Front", "doorbell", true, details: details, ct: CancellationToken.None);

            Assert.Equal("aabbcc", result.RingDeviceId);
            Assert.Equal("doorbell_v4", result.RingKind);
            Assert.Equal("2.0.1", result.FirmwareVersion);
            Assert.Equal(41.5, result.Latitude);
            Assert.Equal(-87.6, result.Longitude);
            Assert.Equal("1 Main St", result.Address);
            Assert.True(result.Subscribed);
            Assert.False(result.SubscribedMotions);
            Assert.Equal("87", result.BatteryLife);
            Assert.True(result.ExternalConnection);
            Assert.True(result.Owned);
            Assert.False(result.Stolen);
        }

        [Fact]
        public async Task EnsureDeviceAsync_WithSettingsScalars_StoresOwnerAndSettingsOnCreate()
        {
            var locationId = Guid.NewGuid();
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = mockDeviceRepoInContext.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));
            var details = new VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields(
                EnableVod: true, ExposureControl: 3, VodStatus: "active", DingAudioId: "d1", DoNotDisturbSecondsLeft: 1800,
                LightBrightness: 80, OwnerId: "42", OwnerFirstName: "Pat", OwnerLastName: "Lee", OwnerEmail: "pat@example.com");

            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-settings", "Porch", "camera", true, details: details, ct: CancellationToken.None);

            Assert.True(result.EnableVod);
            Assert.Equal(3, result.ExposureControl);
            Assert.Equal("active", result.VodStatus);
            Assert.Equal("d1", result.DingAudioId);
            Assert.Equal(1800, result.DoNotDisturbSecondsLeft);
            Assert.Equal(80, result.LightBrightness);
            Assert.Equal("42", result.OwnerId);
            Assert.Equal("Pat", result.OwnerFirstName);
            Assert.Equal("Lee", result.OwnerLastName);
            Assert.Equal("pat@example.com", result.OwnerEmail);
        }

        [Fact]
        public async Task EnsureDeviceAsync_WithRemainingScalars_StoresOnCreate()
        {
            var locationId = Guid.NewGuid();
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = mockDeviceRepoInContext.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));
            var details = new VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields(
                RingType: "doorbot", RingRingId: "abc", BatteryLife2: "55", BatteryVoltage: 12.5m, BatteryVoltage2: 3.3m,
                LedStatus: "on", SirenSecondsRemaining: 30, NightMode: 1, MotionSnoozeJson: "{\"until\":5}",
                SettingsMetadataJson: "{\"k\":1}", LightSettingsMetadataJson: "{\"s\":1}", PirSettingsSensitivity1: 1,
                PirSettingsSensitivity2: 2, PirSettingsSensitivity3: 3, PirSettingsZoneEnable: 1, PirSettingsZoneMask: 15,
                NightModeOn: true, MotionDetectionEnabled: false, SettingsChimeType: 2, SettingsChimeEnabled: true, SettingsChimeDuration: 3);

            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-remaining", "Porch", "camera", true, details: details, ct: CancellationToken.None);

            Assert.Equal("doorbot", result.RingType);
            Assert.Equal("abc", result.RingRingId);
            Assert.Equal("55", result.BatteryLife2);
            Assert.Equal(12.5m, result.BatteryVoltage);
            Assert.Equal(3.3m, result.BatteryVoltage2);
            Assert.Equal("on", result.LedStatus);
            Assert.Equal(30, result.SirenSecondsRemaining);
            Assert.Equal(1, result.NightMode);
            Assert.Equal("{\"until\":5}", result.MotionSnoozeJson);
            Assert.Equal("{\"k\":1}", result.SettingsMetadataJson);
            Assert.Equal("{\"s\":1}", result.LightSettingsMetadataJson);
            Assert.Equal(2, result.PirSettingsSensitivity2);
            Assert.Equal(15, result.PirSettingsZoneMask);
            Assert.True(result.NightModeOn);
            Assert.False(result.MotionDetectionEnabled);
            Assert.Equal(2, result.SettingsChimeType);
            Assert.True(result.SettingsChimeEnabled);
            Assert.Equal(3, result.SettingsChimeDuration);
        }

        [Fact]
        public async Task EnsureDeviceAsync_ExistingDeviceWithoutDetailFields_LeavesStoredDetailFieldsUntouched()
        {
            var locationId = Guid.NewGuid();
            var existing = new Device
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ProviderDeviceId = "device-keep",
                Name = "Front",
                Type = "doorbell",
                RingDeviceId = "keepme",
                Owned = true
            };
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));

            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-keep", "Front", "doorbell", true, ct: CancellationToken.None);

            Assert.Equal("keepme", result.RingDeviceId);
            Assert.True(result.Owned);
        }

        [Fact]
        public async Task EnsureDeviceAsync_WithTimeZone_StoresTimeZoneIdOnCreate()
        {
            // Arrange: Ring's device time_zone must land in Devices.TimeZoneId (it was previously dropped).
            var locationId = Guid.NewGuid();
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = mockDeviceRepoInContext.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-tz", "Front", "doorbell", true, timeZoneId: "America/New_York", ct: CancellationToken.None);

            // Assert
            Assert.Equal("America/New_York", result.TimeZoneId);
        }

        [Fact]
        public async Task EnsureDeviceAsync_ExistingDeviceWithTimeZone_UpdatesTimeZoneId()
        {
            var locationId = Guid.NewGuid();
            var existing = new Device { Id = Guid.NewGuid(), LocationId = locationId, ProviderDeviceId = "device-tz2", Name = "Front", Type = "doorbell", TimeZoneId = null };
            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockDeviceRepoInContext = new Mock<IDeviceRepository>();
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) => await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, "device-tz2", "Front", "doorbell", true, timeZoneId: "Europe/London", ct: CancellationToken.None);

            // Assert
            Assert.Equal("Europe/London", result.TimeZoneId);
        }

        [Fact]
        public async Task RecordDeviceDetailsAsync_DelegatesToDeviceDetailsRepository()
        {
            Guid deviceId = Guid.NewGuid();
            Guid healthId = Guid.NewGuid();
            var caps = new DeviceCapabilities { Id = Guid.NewGuid(), DeviceId = deviceId };

            await _dataClient.RecordDeviceDetailsAsync(deviceId, healthId, caps, null, null, CancellationToken.None);

            _mockDeviceDetailsRepository.Verify(r => r.UpsertAsync(deviceId, healthId, caps, null, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceAsync_CreatesNewDeviceWhenMovedToNewLocation()
        {
            // Arrange: device was previously recorded under a placeholder/synthetic location.
            // When the same ProviderDeviceId appears in a different location, we create a NEW
            // Device record (not relocate). This preserves forensic history: events at the old
            // location still reference the old device record.
            var placeholderLocationId = Guid.NewGuid();
            var realLocationId = Guid.NewGuid();
            string providerDeviceId = "device-789";

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

            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            // Not found under the real location...
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(realLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            // ...but found via the account-wide fallback lookup, still under the old location.
            _ = mockDeviceRepoInContext
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingDevice]);
            // No device exists at the real location yet.
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByProviderDeviceIdAsync(realLocationId, providerDeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Device?)null);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
                realLocationId, providerDeviceId, "Front Camera", "camera", true, ct: CancellationToken.None);

            // Assert: NEW device created with different Id, both at real location.
            // Old device remains at placeholder location for history preservation.
            Assert.NotEqual(existingDevice.Id, result.Id);
            Assert.Equal(realLocationId, result.LocationId);
            Assert.Equal(providerDeviceId, result.ProviderDeviceId);

            mockDeviceRepoInContext.Verify(
                x => x.AddAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Once);
            mockDeviceRepoInContext.Verify(
                x => x.UpdateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task VideoForensicsDataClient_EnsureDevice_KeepsNameWhenIncomingNameIsId()
        {
            var locationId = Guid.NewGuid();
            string providerDeviceId = "device-keepname";
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
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingDevice]);
            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act: caller falls back to the provider ID as the name after a failed name lookup.
            Device result = await _dataClient.EnsureDeviceAsync(
                locationId, providerDeviceId, providerDeviceId, "camera", true, ct: CancellationToken.None);

            Assert.Equal("Front Camera", result.Name);
        }

        [Fact]
        public async Task EnsureDeviceAsync_UpdatesExistingDevice()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            string providerDeviceId = "device-456";
            string newDeviceName = "Front Camera Updated";
            string newDeviceType = "doorbell";
            bool isOnline = false;

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

            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);

            _ = mockDeviceRepoInContext
                .Setup(x => x.GetByLocationIdAsync(locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingDevice]);

            _ = _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<Device>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<Device>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            Device result = await _dataClient.EnsureDeviceAsync(
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
            DateTime newerTimestamp = currentWatermark.AddHours(1);
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
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            _ = _mockUnitOfWork
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
            DateTime olderTimestamp = currentWatermark.AddHours(-1);
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
            _ = mockContext.Setup(x => x.Devices).Returns(mockDeviceRepoInContext.Object);
            _ = mockDeviceRepoInContext.Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);

            _ = _mockUnitOfWork
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
