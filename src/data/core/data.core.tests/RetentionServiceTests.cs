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
    public class RetentionServiceTests
    {
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IActionLogger> _mockActionLogger;
        private readonly Mock<ILogger<RetentionService>> _mockLogger;
        private readonly int _retentionDays = 90;

        private RetentionService CreateService()
        {
            return new RetentionService(
                _mockMediaItemRepository.Object,
                _mockUnitOfWork.Object,
                _mockActionLogger.Object,
                _mockLogger.Object,
                _retentionDays);
        }

        public RetentionServiceTests()
        {
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockActionLogger = new Mock<IActionLogger>();
            _mockLogger = new Mock<ILogger<RetentionService>>();
        }

        [Fact]
        public async Task PurgeExpiredAsync_PurgesItemsOlderThanRetentionThreshold()
        {
            // Arrange
            var service = CreateService();
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            var expiredItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "expired.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "expired.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = cutoffDate.AddDays(-1),
                DownloadedAtUtc = cutoffDate.AddDays(-1),
                IsPurged = false
            };

            var recentItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "recent.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "recent.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "def456",
                RecordedAtUtc = cutoffDate.AddDays(1),
                DownloadedAtUtc = cutoffDate.AddDays(1),
                IsPurged = false
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { expiredItem, recentItem });

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            var expectedLogEntry = new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = Environment.UserName,
                ActorType = ActorType.Human,
                Action = "MediaPurged",
                EntityType = "MediaItem",
                EntityId = expiredItem.Id,
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
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await service.PurgeExpiredAsync(CancellationToken.None);

            // Assert
            Assert.Equal(1, result);
            mockMediaItemRepoInContext.Verify(
                x => x.UpdateAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PurgeExpiredAsync_SetsPurgeFieldsCorrectly()
        {
            // Arrange
            var service = CreateService();
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            var expiredItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "expired.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "expired.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = cutoffDate.AddDays(-1),
                DownloadedAtUtc = cutoffDate.AddDays(-1),
                IsPurged = false
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { expiredItem });

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestHelpers.CreateActionLogEntry());

            MediaItem updatedItem = null!;
            mockMediaItemRepoInContext
                .Setup(x => x.UpdateAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .Callback<MediaItem, CancellationToken>((item, ct) => { updatedItem = item; })
                .Returns(Task.CompletedTask);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await service.PurgeExpiredAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(updatedItem);
            Assert.True(updatedItem.IsPurged);
            Assert.NotNull(updatedItem.PurgedAtUtc);
            Assert.NotNull(updatedItem.PurgeReason);
            Assert.Contains("Retention policy", updatedItem.PurgeReason);
            Assert.Contains("90", updatedItem.PurgeReason); // Contains retention days
        }

        [Fact]
        public async Task PurgeExpiredAsync_SkipsAlreadyPurgedItems()
        {
            // Arrange
            var service = CreateService();
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            var alreadyPurgedItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "already_purged.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "already_purged.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = cutoffDate.AddDays(-1),
                DownloadedAtUtc = cutoffDate.AddDays(-1),
                IsPurged = true,
                PurgedAtUtc = cutoffDate
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { alreadyPurgedItem });

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await service.PurgeExpiredAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
            mockMediaItemRepoInContext.Verify(
                x => x.UpdateAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PurgeExpiredAsync_WithNoExpiredItems_ReturnsZero()
        {
            // Arrange
            var service = CreateService();
            var recentItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "recent.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "recent.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "def456",
                RecordedAtUtc = DateTime.UtcNow.AddDays(-10),
                DownloadedAtUtc = DateTime.UtcNow.AddDays(-10),
                IsPurged = false
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { recentItem });

            var mockContext = new Mock<IUnitOfWorkContext>();
            mockContext.Setup(x => x.MediaItems).Returns(new Mock<IMediaItemRepository>().Object);

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            var result = await service.PurgeExpiredAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task PurgeExpiredAsync_LogsActionForEachPurgedItem()
        {
            // Arrange
            var service = CreateService();
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            var expiredItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "expired.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "expired.mp4"),
                MediaFormat = "video/mp4",
                Sha256Hash = "abc123",
                RecordedAtUtc = cutoffDate.AddDays(-1),
                DownloadedAtUtc = cutoffDate.AddDays(-1),
                IsPurged = false
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem> { expiredItem });

            var mockContext = new Mock<IUnitOfWorkContext>();
            var mockMediaItemRepoInContext = new Mock<IMediaItemRepository>();
            var mockActionLogRepoInContext = new Mock<IActionLogRepository>();

            mockContext.Setup(x => x.MediaItems).Returns(mockMediaItemRepoInContext.Object);
            mockContext.Setup(x => x.ActionLog).Returns(mockActionLogRepoInContext.Object);

            mockActionLogRepoInContext
                .Setup(x => x.AppendAsync(
                    It.IsAny<string>(),
                    It.IsAny<ActorType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestHelpers.CreateActionLogEntry());

            _mockUnitOfWork
                .Setup(x => x.ExecuteAsync(
                    It.IsAny<Func<IUnitOfWorkContext, Task<bool>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (Func<IUnitOfWorkContext, Task<bool>> work, CancellationToken ct) =>
                    await work(mockContext.Object));

            // Act
            await service.PurgeExpiredAsync(CancellationToken.None);

            // Assert
            mockActionLogRepoInContext.Verify(
                x => x.AppendAsync(
                    Environment.UserName,
                    ActorType.Human,
                    "MediaPurged",
                    "MediaItem",
                    expiredItem.Id,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
