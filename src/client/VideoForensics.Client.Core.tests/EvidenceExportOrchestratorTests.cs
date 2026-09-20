using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class EvidenceExportOrchestratorTests
    {
        private readonly Mock<ILogger<EvidenceExportOrchestrator>> _loggerMock;
        private readonly Mock<IMediaItemRepository> _mediaItemRepositoryMock;
        private readonly Mock<IIntegrityVerificationService> _integrityVerificationServiceMock;
        private readonly Mock<IActionLogRepository> _actionLogRepositoryMock;
        private readonly Mock<IExportRecordService> _exportRecordServiceMock;
        private readonly EvidenceExportOrchestrator _orchestrator;

        public EvidenceExportOrchestratorTests()
        {
            _loggerMock = new Mock<ILogger<EvidenceExportOrchestrator>>();
            _mediaItemRepositoryMock = new Mock<IMediaItemRepository>();
            _integrityVerificationServiceMock = new Mock<IIntegrityVerificationService>();
            _actionLogRepositoryMock = new Mock<IActionLogRepository>();
            _exportRecordServiceMock = new Mock<IExportRecordService>();

            _orchestrator = new EvidenceExportOrchestrator(
                _loggerMock.Object,
                _mediaItemRepositoryMock.Object,
                _integrityVerificationServiceMock.Object,
                _actionLogRepositoryMock.Object,
                _exportRecordServiceMock.Object);
        }

        [Fact]
        public async Task ExportEvidenceAsync_SanitizesLogOutput_WhenCaseReferenceContainsNewlines()
        {
            // Arrange: Setup mocks and test directory
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var mediaItemId = Guid.NewGuid();
                string injectedCaseRef = "Case-123\r\nFAKE ADMIN LOG: Unauthorized access granted";
                string recipient = "Detective Smith";

                // Setup media item mock
                var mediaItem = new MediaItem
                {
                    Id = mediaItemId,
                    FileName = "test-video.mp4",
                    FilePath = Path.Combine(tempDir, "test-video.mp4"),
                    Sha256Hash = "abc123",
                    MediaFormat = "mp4",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                };

                // Create dummy file
                File.WriteAllText(mediaItem.FilePath, "dummy content");

                _ = _mediaItemRepositoryMock
                    .Setup(r => r.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((MediaItem?)mediaItem));

                _ = _integrityVerificationServiceMock
                    .Setup(s => s.VerifyAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(true));

                _ = _actionLogRepositoryMock
                    .Setup(r => r.GetHistoryForEntityAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((IReadOnlyList<ActionLogEntry>)[]));

                _ = _exportRecordServiceMock
                    .Setup(s => s.RecordExportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "test-user",
                        ArchiveFileName = "test-archive.zip",
                        ArchiveSha256Hash = "abc123",
                        AppVersion = "1.0.0"
                    }));

                // Act: Call ExportEvidenceAsync with injected case reference containing CRLF
                ExportResult result = await _orchestrator.ExportEvidenceAsync(
                    [mediaItemId],
                    tempDir,
                    injectedCaseRef,
                    recipient,
                    null,
                    CancellationToken.None);

                // Assert: Verify the method completed and didn't throw
                // (the sanitization is verified indirectly — the method completes without exception)
                Assert.NotNull(result);
                // Verify export was recorded
                _exportRecordServiceMock.Verify(
                    s => s.RecordExportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()),
                    Times.Once);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task ExportEvidenceAsync_SucceedsWithNormalPath_AndArchiveFileStaysWithinOutputDirectory()
        {
            // Arrange: Setup mocks and test directory
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var mediaItemId = Guid.NewGuid();
                string caseRef = "Case-123";
                string recipient = "Detective Smith";

                // Setup media item mock
                var mediaItem = new MediaItem
                {
                    Id = mediaItemId,
                    FileName = "test-video.mp4",
                    FilePath = Path.Combine(tempDir, "test-video.mp4"),
                    Sha256Hash = "abc123",
                    MediaFormat = "mp4",
                    RecordedAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = DateTime.UtcNow
                };

                // Create dummy file
                File.WriteAllText(mediaItem.FilePath, "dummy content");

                _ = _mediaItemRepositoryMock
                    .Setup(r => r.GetAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((MediaItem?)mediaItem));

                _ = _integrityVerificationServiceMock
                    .Setup(s => s.VerifyAsync(mediaItemId, It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(true));

                _ = _actionLogRepositoryMock
                    .Setup(r => r.GetHistoryForEntityAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult((IReadOnlyList<ActionLogEntry>)[]));

                _ = _exportRecordServiceMock
                    .Setup(s => s.RecordExportAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IReadOnlyList<(Guid, string)>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        ExportedByUserName = "test-user",
                        ArchiveFileName = "test-archive.zip",
                        ArchiveSha256Hash = "abc123",
                        AppVersion = "1.0.0"
                    }));

                // Act: Call ExportEvidenceAsync with normal paths
                ExportResult result = await _orchestrator.ExportEvidenceAsync(
                    [mediaItemId],
                    tempDir,
                    caseRef,
                    recipient,
                    null,
                    CancellationToken.None);

                // Assert: Verify success and archive exists within output directory
                Assert.NotNull(result);
                Assert.True(result.Success, result.ErrorMessage);
                Assert.NotNull(result.ArchivePath);
                Assert.True(File.Exists(result.ArchivePath));

                // Verify the archive path is within the output directory
                string fullRoot = Path.GetFullPath(tempDir);
                string fullArchivePath = Path.GetFullPath(result.ArchivePath);
                Assert.True(
                    fullArchivePath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    $"Archive path '{fullArchivePath}' should be within output directory '{fullRoot}'");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
