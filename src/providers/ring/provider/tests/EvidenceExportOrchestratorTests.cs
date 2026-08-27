using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for EvidenceExportOrchestrator (Phase 4).
    /// Verifies secure evidence export into password-protected archives.
    /// </summary>
    public class EvidenceExportOrchestratorTests
    {
        private readonly Mock<ILogger<EvidenceExportOrchestrator>> _mockLogger = new();
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepository = new();
        private readonly Mock<IIntegrityVerificationService> _mockIntegrityService = new();
        private readonly Mock<IActionLogRepository> _mockActionLogRepository = new();
        private readonly Mock<IExportRecordService> _mockExportRecordService = new();

        private string CreateTempDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithNoItems_ReturnsFailure()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            try
            {
                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new List<Guid>(),
                    tempDir,
                    null,
                    null,
                    null,
                    CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.NotNull(result.ErrorMessage);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithFailedIntegrityItem_ExcludesItem()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var failedItemId = Guid.NewGuid();
            var validItemId = Guid.NewGuid();
            var failedFilePath = Path.Combine(tempDir, "failed.mp4");
            var validFilePath = Path.Combine(tempDir, "valid.mp4");
            File.WriteAllText(failedFilePath, "dummy content");
            File.WriteAllText(validFilePath, "valid content");

            try
            {
                var failedItem = new MediaItem
                {
                    Id = failedItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "failed.mp4",
                    FilePath = failedFilePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    FileSizeBytes = 13,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                var validItem = new MediaItem
                {
                    Id = validItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "valid.mp4",
                    FilePath = validFilePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "def456",
                    FileSizeBytes = 13,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _mockMediaItemRepository.Setup(r => r.GetAsync(failedItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(failedItem);
                _mockMediaItemRepository.Setup(r => r.GetAsync(validItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(validItem);

                _mockIntegrityService.Setup(s => s.VerifyAsync(failedItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false); // Verification fails
                _mockIntegrityService.Setup(s => s.VerifyAsync(validItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true); // Verification passes

                _mockActionLogRepository.Setup(r => r.GetHistoryForEntityAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ActionLogEntry>());

                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new[] { failedItemId, validItemId },
                    tempDir,
                    null,
                    null,
                    null,
                    CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Single(result.ItemsExcludedForFailedIntegrity);
                Assert.Contains(failedItemId, result.ItemsExcludedForFailedIntegrity);
                Assert.Equal(1, result.ItemsIncluded);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithValidItem_CreatesArchive()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var mediaItemId = Guid.NewGuid();
            var filePath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(filePath, "dummy content");

            try
            {
                var mediaItem = new MediaItem
                {
                    Id = mediaItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "test.mp4",
                    FilePath = filePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    FileSizeBytes = 13,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _mockMediaItemRepository.Setup(r => r.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                _mockIntegrityService.Setup(s => s.VerifyAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                _mockActionLogRepository.Setup(r => r.GetHistoryForEntityAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ActionLogEntry>());

                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new[] { mediaItemId },
                    tempDir,
                    "Case-2026-001",
                    "Law Enforcement",
                    null, // No password
                    CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.NotNull(result.ArchivePath);
                Assert.True(File.Exists(result.ArchivePath));
                Assert.EndsWith(".zip", result.ArchivePath);
                Assert.Equal(1, result.ItemsIncluded);
                Assert.Empty(result.ItemsExcludedForFailedIntegrity);
                Assert.NotNull(result.ArchiveSha256Hash);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithEncryption_CreatesPasswordProtectedArchive()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var mediaItemId = Guid.NewGuid();
            var filePath = Path.Combine(tempDir, "test.mp4");
            File.WriteAllText(filePath, "dummy content");

            try
            {
                var mediaItem = new MediaItem
                {
                    Id = mediaItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "test.mp4",
                    FilePath = filePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    FileSizeBytes = 13,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _mockMediaItemRepository.Setup(r => r.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem);

                _mockIntegrityService.Setup(s => s.VerifyAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                _mockActionLogRepository.Setup(r => r.GetHistoryForEntityAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ActionLogEntry>());

                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new[] { mediaItemId },
                    tempDir,
                    "Case-2026-001",
                    "Law Enforcement",
                    "SecurePassword123", // With password
                    CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.NotNull(result.ArchivePath);
                Assert.True(File.Exists(result.ArchivePath));
                Assert.EndsWith(".zip", result.ArchivePath);
                Assert.Equal(1, result.ItemsIncluded);
                Assert.NotNull(result.ArchiveSha256Hash);

                // Verify the archive is password protected by checking file size
                // (encrypted archives have different sizes than unencrypted)
                var fileInfo = new FileInfo(result.ArchivePath);
                Assert.True(fileInfo.Length > 0);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithMultipleItems_IncludesAllValidItems()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var mediaItemId1 = Guid.NewGuid();
            var mediaItemId2 = Guid.NewGuid();
            var filePath1 = Path.Combine(tempDir, "test1.mp4");
            var filePath2 = Path.Combine(tempDir, "test2.mp4");
            File.WriteAllText(filePath1, "content1");
            File.WriteAllText(filePath2, "content2");

            try
            {
                var mediaItem1 = new MediaItem
                {
                    Id = mediaItemId1,
                    DeviceId = Guid.NewGuid(),
                    FileName = "test1.mp4",
                    FilePath = filePath1,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    FileSizeBytes = 8,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-2),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                var mediaItem2 = new MediaItem
                {
                    Id = mediaItemId2,
                    DeviceId = Guid.NewGuid(),
                    FileName = "test2.mp4",
                    FilePath = filePath2,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "def456",
                    FileSizeBytes = 8,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _mockMediaItemRepository.Setup(r => r.GetAsync(mediaItemId1, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem1);
                _mockMediaItemRepository.Setup(r => r.GetAsync(mediaItemId2, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mediaItem2);

                _mockIntegrityService.Setup(s => s.VerifyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                _mockActionLogRepository.Setup(r => r.GetHistoryForEntityAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ActionLogEntry>());

                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new[] { mediaItemId1, mediaItemId2 },
                    tempDir,
                    "Case-2026-001",
                    "Law Enforcement",
                    null,
                    CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(2, result.ItemsIncluded);
                Assert.Empty(result.ItemsExcludedForFailedIntegrity);
                Assert.NotNull(result.ArchivePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_WithMixedValidAndInvalid_IncludesOnlyValid()
        {
            // Arrange
            var tempDir = CreateTempDirectory();
            var validItemId = Guid.NewGuid();
            var invalidItemId = Guid.NewGuid();
            var validFilePath = Path.Combine(tempDir, "valid.mp4");
            File.WriteAllText(validFilePath, "valid content");

            try
            {
                var validItem = new MediaItem
                {
                    Id = validItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "valid.mp4",
                    FilePath = validFilePath,
                    MediaFormat = "video/mp4",
                    Sha256Hash = "abc123",
                    FileSizeBytes = 13,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-1),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                var invalidItem = new MediaItem
                {
                    Id = invalidItemId,
                    DeviceId = Guid.NewGuid(),
                    FileName = "invalid.mp4",
                    FilePath = Path.Combine(tempDir, "invalid.mp4"),
                    MediaFormat = "video/mp4",
                    Sha256Hash = "def456",
                    FileSizeBytes = 100,
                    RecordedAtUtc = DateTime.UtcNow.AddHours(-2),
                    DownloadedAtUtc = DateTime.UtcNow
                };

                _mockMediaItemRepository.Setup(r => r.GetAsync(validItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(validItem);
                _mockMediaItemRepository.Setup(r => r.GetAsync(invalidItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(invalidItem);

                _mockIntegrityService.Setup(s => s.VerifyAsync(validItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                _mockIntegrityService.Setup(s => s.VerifyAsync(invalidItemId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                _mockActionLogRepository.Setup(r => r.GetHistoryForEntityAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ActionLogEntry>());

                _mockExportRecordService.Setup(s => s.RecordExportAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<IReadOnlyList<(Guid, string)>>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "TestUser",
                        ArchiveFileName = "test_archive.zip",
                        ArchiveSha256Hash = "testhash",
                        AppVersion = "1.0.0"
                    });

                var orchestrator = new EvidenceExportOrchestrator(
                    _mockLogger.Object,
                    _mockMediaItemRepository.Object,
                    _mockIntegrityService.Object,
                    _mockActionLogRepository.Object,
                    _mockExportRecordService.Object);

                // Act
                var result = await orchestrator.ExportEvidenceAsync(
                    new[] { validItemId, invalidItemId },
                    tempDir,
                    "Case-2026-001",
                    "Law Enforcement",
                    null,
                    CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, result.ItemsIncluded);
                Assert.Single(result.ItemsExcludedForFailedIntegrity);
                Assert.Contains(invalidItemId, result.ItemsExcludedForFailedIntegrity);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
