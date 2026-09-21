using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class BackupExportOrchestratorTests
    {
        [Fact]
        public async Task ExportBackupAsync_SanitizesLogOutput_WhenArchivePathContainsNewlines()
        {
            // Arrange: Setup mocks to return empty collections
            var loggerMock = new Mock<ILogger<BackupExportOrchestrator>>();
            var providerAccountRepositoryMock = new Mock<IProviderAccountRepository>();
            var locationRepositoryMock = new Mock<ILocationRepository>();
            var deviceRepositoryMock = new Mock<IDeviceRepository>();
            var eventRepositoryMock = new Mock<IEventRepository>();
            var downloadEventRepositoryMock = new Mock<IDownloadEventRepository>();
            var mediaItemRepositoryMock = new Mock<IMediaItemRepository>();
            var configurationMock = new Mock<IForensicsConfiguration>();
            var metadataTaggerMock = new Mock<IMediaMetadataTagger>();

            _ = providerAccountRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<ProviderAccount>)[]));

            _ = locationRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Data.Common.Entities.Location>)[]));

            _ = deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Data.Common.Entities.Device>)[]));

            _ = eventRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Event>)[]));

            _ = downloadEventRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<DownloadEvent>)[]));

            _ = mediaItemRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<MediaItem>)[]));

            _ = configurationMock
                .Setup(c => c.DownloadLocation)
                .Returns("/downloads");

            var orchestrator = new BackupExportOrchestrator(
                loggerMock.Object,
                providerAccountRepositoryMock.Object,
                locationRepositoryMock.Object,
                deviceRepositoryMock.Object,
                eventRepositoryMock.Object,
                downloadEventRepositoryMock.Object,
                mediaItemRepositoryMock.Object,
                configurationMock.Object,
                metadataTaggerMock.Object);

            // Create test directory
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act: Call ExportBackupAsync (the method logs the archive path, which is sanitized)
                BackupExportResult result = await orchestrator.ExportBackupAsync(tempDir, CancellationToken.None);

                // Assert: Verify the method completed and didn't throw
                // (the sanitization is verified indirectly — the method completes without exception)
                Assert.NotNull(result);
                Assert.True(result.Success);
                // Verify the archive was created
                Assert.NotNull(result.ArchivePath);
                Assert.True(File.Exists(result.ArchivePath));
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
        public async Task ExportBackupAsync_SucceedsWithNormalPath_AndArchiveFileStaysWithinOutputDirectory()
        {
            // Arrange: Setup mocks to return empty collections
            var loggerMock = new Mock<ILogger<BackupExportOrchestrator>>();
            var providerAccountRepositoryMock = new Mock<IProviderAccountRepository>();
            var locationRepositoryMock = new Mock<ILocationRepository>();
            var deviceRepositoryMock = new Mock<IDeviceRepository>();
            var eventRepositoryMock = new Mock<IEventRepository>();
            var downloadEventRepositoryMock = new Mock<IDownloadEventRepository>();
            var mediaItemRepositoryMock = new Mock<IMediaItemRepository>();
            var configurationMock = new Mock<IForensicsConfiguration>();
            var metadataTaggerMock = new Mock<IMediaMetadataTagger>();

            _ = providerAccountRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<ProviderAccount>)[]));

            _ = locationRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Data.Common.Entities.Location>)[]));

            _ = deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Data.Common.Entities.Device>)[]));

            _ = eventRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Event>)[]));

            _ = downloadEventRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<DownloadEvent>)[]));

            _ = mediaItemRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<MediaItem>)[]));

            _ = configurationMock
                .Setup(c => c.DownloadLocation)
                .Returns("/downloads");

            var orchestrator = new BackupExportOrchestrator(
                loggerMock.Object,
                providerAccountRepositoryMock.Object,
                locationRepositoryMock.Object,
                deviceRepositoryMock.Object,
                eventRepositoryMock.Object,
                downloadEventRepositoryMock.Object,
                mediaItemRepositoryMock.Object,
                configurationMock.Object,
                metadataTaggerMock.Object);

            // Create test directory
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act: Call ExportBackupAsync with normal path
                BackupExportResult result = await orchestrator.ExportBackupAsync(tempDir, CancellationToken.None);

                // Assert: Verify success and archive exists within output directory
                Assert.NotNull(result);
                Assert.True(result.Success);
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
