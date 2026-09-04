using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Models;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class ReportGenerationServiceTests
    {
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository;
        private readonly Mock<IDeviceRepository> _mockDeviceRepository;
        private readonly Mock<IDownloadEventRepository> _mockDownloadEventRepository;
        private readonly Mock<IActionLogRepository> _mockActionLogRepository;
        private readonly Mock<IEventRepository> _mockEventRepository;
        private readonly Mock<IJammingRepository> _mockJammingRepository;
        private readonly Mock<IIntegrityRecordRepository> _mockIntegrityRecordRepository;
        private readonly Mock<ILogger<ReportGenerationService>> _mockLogger;
        private readonly ReportGenerationService _service;

        public ReportGenerationServiceTests()
        {
            _mockMediaItemRepository = new Mock<IMediaItemRepository>();
            _mockDeviceRepository = new Mock<IDeviceRepository>();
            _mockDownloadEventRepository = new Mock<IDownloadEventRepository>();
            _mockActionLogRepository = new Mock<IActionLogRepository>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockJammingRepository = new Mock<IJammingRepository>();
            _mockIntegrityRecordRepository = new Mock<IIntegrityRecordRepository>();
            _mockLogger = new Mock<ILogger<ReportGenerationService>>();

            _mockIntegrityRecordRepository
                .Setup(x => x.GetLatestByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IntegrityRecord>());

            _service = new ReportGenerationService(
                _mockMediaItemRepository.Object,
                _mockDeviceRepository.Object,
                _mockDownloadEventRepository.Object,
                _mockActionLogRepository.Object,
                _mockEventRepository.Object,
                _mockJammingRepository.Object,
                _mockIntegrityRecordRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_WithDeviceId_ReturnsMediaItemsForDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var mediaItems = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "video1.mp4",
                    FilePath = "/path/video1.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    DownloadedAtUtc = new DateTime(2024, 1, 15, 13, 0, 0, DateTimeKind.Utc),
                    IntegrityVerified = true
                },
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "video2.mp4",
                    FilePath = "/path/video2.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash2",
                    RecordedAtUtc = new DateTime(2024, 1, 20, 12, 0, 0, DateTimeKind.Utc),
                    DownloadedAtUtc = new DateTime(2024, 1, 20, 13, 0, 0, DateTimeKind.Utc),
                    IntegrityVerified = false
                }
            };

            _mockMediaItemRepository
                .Setup(x => x.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaItems);

            // Act
            var result = await _service.BuildEvidenceReviewAsync(deviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(fromUtc, result.ReportFromUtc);
            Assert.Equal(toUtc, result.ReportToUtc);
            Assert.Equal(2, result.TotalItemCount);
            Assert.Equal(1, result.VerifiedItemCount);
            Assert.Equal(2, result.MediaItems.Count);
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_WithoutDeviceId_ReturnsAllMediaItemsInRange()
        {
            // Arrange
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var mediaItems = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    FileName = "video1.mp4",
                    FilePath = "/path/video1.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    DownloadedAtUtc = new DateTime(2024, 1, 15, 13, 0, 0, DateTimeKind.Utc),
                    IntegrityVerified = true
                }
            };

            _mockMediaItemRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaItems);

            // Act
            var result = await _service.BuildEvidenceReviewAsync(null, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.TotalItemCount);
            Assert.Equal(1, result.VerifiedItemCount);
        }

        [Fact]
        public async Task BuildForensicAnalysisReportAsync_ReturnsReport()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var mediaItems = new List<MediaItem>
            {
                new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    FileName = "evidence.mp4",
                    FilePath = "/path/evidence.mp4",
                    MediaFormat = "video/mp4",
                    Sha256Hash = "hash1",
                    RecordedAtUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    DownloadedAtUtc = new DateTime(2024, 1, 15, 13, 0, 0, DateTimeKind.Utc)
                }
            };

            _mockMediaItemRepository
                .Setup(x => x.GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mediaItems);

            // Act
            var result = await _service.BuildForensicAnalysisReportAsync(deviceId, fromUtc, toUtc, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(fromUtc, result.ReportFromUtc);
            Assert.Equal(toUtc, result.ReportToUtc);
            Assert.Equal(1, result.EvidenceItems.Count);
            Assert.NotNull(result.Summary);
        }

        [Fact]
        public async Task BuildSignalAnomalyReportAsync_WithDeviceId_ReturnsAnomaliesForDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var device = new Device
            {
                Id = deviceId,
                LocationId = Guid.NewGuid(),
                Name = "Front Door",
                ProviderDeviceId = "ring-device-1",
                Type = "Doorbell",
                IsOnline = true
            };

            _mockDeviceRepository
                .Setup(x => x.GetAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(device);

            // Act
            var result = await _service.BuildSignalAnomalyReportAsync(
                deviceId,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.AnomaliesByDevice);
            Assert.Equal(deviceId, result.AnomaliesByDevice[0].DeviceId);
            Assert.Equal("Front Door", result.AnomaliesByDevice[0].DeviceName);
        }

        [Fact]
        public async Task BuildSignalAnomalyReportAsync_WithoutDeviceId_ReturnsAnomaliesForAllDevices()
        {
            // Arrange
            var devices = new List<Device>
            {
                new Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    Name = "Front Door",
                    ProviderDeviceId = "ring-1",
                    Type = "Doorbell",
                    IsOnline = true
                },
                new Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    Name = "Back Door",
                    ProviderDeviceId = "ring-2",
                    Type = "Doorbell",
                    IsOnline = false
                }
            };

            _mockDeviceRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            // Act
            var result = await _service.BuildSignalAnomalyReportAsync(
                null,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.AnomaliesByDevice.Count);
        }

        [Fact]
        public async Task BuildAccessControlReportAsync_ReturnsAccessEvents()
        {
            // Arrange
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var actionLogEntries = new List<ActionLogEntry>
            {
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "MediaDownloaded",
                    EntityType = "DownloadEvent",
                    EntityId = Guid.NewGuid(),
                    TimestampUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                    DetailsJson = null
                ,
                EntryHash = "test_hash"},
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "EvidenceExported",
                    EntityType = "ExportRecord",
                    EntityId = Guid.NewGuid(),
                    TimestampUtc = new DateTime(2024, 1, 20, 14, 0, 0, DateTimeKind.Utc),
                    DetailsJson = "case-ref"
                ,
                EntryHash = "test_hash"}
            };

            _mockActionLogRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(actionLogEntries);

            // Act
            var result = await _service.BuildAccessControlReportAsync(
                null,
                fromUtc,
                toUtc,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.AccessEvents.Count);
            Assert.Equal("user1", result.AccessEvents[0].Actor);
            Assert.Equal("MediaDownloaded", result.AccessEvents[0].Action);
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_ReturnsAuditTrail()
        {
            // Arrange
            var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            var actionLogEntries = new List<ActionLogEntry>
            {
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "MediaDownloaded",
                    EntityType = "MediaItem",
                    EntityId = Guid.NewGuid(),
                    TimestampUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc)
                ,
                EntryHash = "test_hash"}
            };

            _mockActionLogRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(actionLogEntries);

            _mockActionLogRepository
                .Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BuildChainOfCustodyReportAsync(
                null,
                fromUtc,
                toUtc,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ChainIntegrityVerified);
            Assert.Single(result.AuditTrail);
            Assert.Equal("Valid hash chain", result.ChainVerificationStatus);
        }

        [Fact]
        public async Task BuildChainOfCustodyReportAsync_WithFailedChainVerification_ReturnsFalse()
        {
            // Arrange
            var actionLogEntries = new List<ActionLogEntry>();

            _mockActionLogRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(actionLogEntries);

            _mockActionLogRepository
                .Setup(x => x.VerifyChainIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.BuildChainOfCustodyReportAsync(
                null,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ChainIntegrityVerified);
            Assert.Equal("Chain integrity check failed", result.ChainVerificationStatus);
        }

        [Fact]
        public async Task WriteReportAsync_WithJsonFormat_WritesJsonFile()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"reports_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            var report = new EvidenceReviewReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ReportFromUtc = DateTime.UtcNow.AddDays(-1),
                ReportToUtc = DateTime.UtcNow,
                TotalItemCount = 5
            };

            try
            {
                // Act - This will use the default reports directory
                // We can't easily override it in the service, so we test the behavior
                await _service.WriteReportAsync(report, "json", CancellationToken.None);

                // Assert - The file should be created in the reports directory
                var reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics",
                    "Reports");

                var files = Directory.GetFiles(reportsDir, "EvidenceReviewReport_*.json");
                Assert.NotEmpty(files);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task WriteReportAsync_WithUnsupportedFormat_ThrowsArgumentException()
        {
            // Arrange
            var report = new EvidenceReviewReport
            {
                GeneratedAtUtc = DateTime.UtcNow,
                TotalItemCount = 0
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.WriteReportAsync(report, "pdf", CancellationToken.None));
        }

        [Fact]
        public async Task BuildEvidenceReviewAsync_WithNoMediaItems_ReturnsEmptyReport()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            _mockMediaItemRepository
                .Setup(x => x.GetByDeviceAndDateRangeAsync(
                    deviceId,
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem>());

            // Act
            var result = await _service.BuildEvidenceReviewAsync(
                deviceId,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalItemCount);
            Assert.Equal(0, result.VerifiedItemCount);
            Assert.Empty(result.MediaItems);
        }

        [Fact]
        public async Task BuildAccessControlReportAsync_FiltersActionsByTimeRange()
        {
            // Arrange
            var fromUtc = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = new DateTime(2024, 1, 20, 23, 59, 59, DateTimeKind.Utc);

            var allActions = new List<ActionLogEntry>
            {
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "Action1",
                    EntityType = "Entity",
                    TimestampUtc = new DateTime(2024, 1, 5, 12, 0, 0, DateTimeKind.Utc) // Before range
                ,
                EntryHash = "test_hash"},
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "Action2",
                    EntityType = "Entity",
                    TimestampUtc = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc) // In range
                ,
                EntryHash = "test_hash"},
                new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "user1",
                    ActorType = ActorType.Human,
                    Action = "Action3",
                    EntityType = "Entity",
                    TimestampUtc = new DateTime(2024, 1, 25, 12, 0, 0, DateTimeKind.Utc) // After range
                ,
                EntryHash = "test_hash"}
            };

            _mockActionLogRepository
                .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(allActions);

            // Act
            var result = await _service.BuildAccessControlReportAsync(
                null,
                fromUtc,
                toUtc,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.AccessEvents);
            Assert.Equal("Action2", result.AccessEvents[0].Action);
        }
    }
}
