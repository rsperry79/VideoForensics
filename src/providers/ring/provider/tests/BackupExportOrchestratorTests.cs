using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class BackupExportOrchestratorTests
    {
        private readonly Mock<ILogger<BackupExportOrchestrator>> _mockLogger = new();
        private readonly Mock<IProviderAccountRepository> _mockAccountRepo = new();
        private readonly Mock<ILocationRepository> _mockLocationRepo = new();
        private readonly Mock<IDeviceRepository> _mockDeviceRepo = new();
        private readonly Mock<IEventRepository> _mockEventRepo = new();
        private readonly Mock<IDownloadEventRepository> _mockDownloadEventRepo = new();
        private readonly Mock<IMediaItemRepository> _mockMediaItemRepo = new();
        private readonly Mock<IForensicsConfiguration> _mockConfig = new();
        private readonly Mock<IMediaMetadataTagger> _mockTagger = new();

        private BackupExportOrchestrator CreateOrchestrator()
        {
            return new(
            _mockLogger.Object,
            _mockAccountRepo.Object,
            _mockLocationRepo.Object,
            _mockDeviceRepo.Object,
            _mockEventRepo.Object,
            _mockDownloadEventRepo.Object,
            _mockMediaItemRepo.Object,
            _mockConfig.Object,
            _mockTagger.Object);
        }

        private static string CreateTempDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        [Fact]
        public async Task PrepareForExportAsync_WithNoDownloadedEvents_ReturnsEmptyResult()
        {
            _ = _mockEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            BackupExportOrchestrator orchestrator = CreateOrchestrator();
            PrepareExportResult result = await orchestrator.PrepareForExportAsync(CancellationToken.None);

            Assert.Equal(0, result.Validated);
            Assert.Equal(0, result.Backfilled);
            Assert.Equal(0, result.MissingMedia);
        }

        [Fact]
        public async Task PrepareForExportAsync_MediaFileMissing_CountsAsMissingMedia()
        {
            var deviceId = Guid.NewGuid();
            var evt = new Event { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", EventType = "motion", DownloadedAtUtc = DateTime.UtcNow };
            var downloadEvent = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", AppVersion = "1.0" };
            var mediaItem = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                DownloadEventId = downloadEvent.Id,
                FileName = "missing.mp4",
                FilePath = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".mp4"),
                MediaFormat = "mp4",
                Sha256Hash = "abc"
            };

            _ = _mockEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([evt]);
            _ = _mockDownloadEventRepo.Setup(r => r.GetByProviderEventIdAsync(deviceId, "evt-1", It.IsAny<CancellationToken>())).ReturnsAsync(downloadEvent);
            _ = _mockMediaItemRepo.Setup(r => r.GetByDownloadEventIdAsync(downloadEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync([mediaItem]);

            BackupExportOrchestrator orchestrator = CreateOrchestrator();
            PrepareExportResult result = await orchestrator.PrepareForExportAsync(CancellationToken.None);

            Assert.Equal(1, result.MissingMedia);
            Assert.Equal(0, result.Validated);
        }

        [Fact]
        public async Task PrepareForExportAsync_MatchingSidecar_CountsAsValidated()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                var deviceId = Guid.NewGuid();
                var evt = new Event { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", EventType = "motion", DownloadedAtUtc = DateTime.UtcNow };
                var downloadEvent = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", AppVersion = "1.0" };
                string mediaFilePath = Path.Combine(tempDir, "clip.mp4");
                File.WriteAllText(mediaFilePath, "dummy");
                File.WriteAllText(Path.ChangeExtension(mediaFilePath, ".json"), $"{{\"EventDbId\": \"{evt.Id}\"}}");

                var mediaItem = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    DownloadEventId = downloadEvent.Id,
                    FileName = "clip.mp4",
                    FilePath = mediaFilePath,
                    MediaFormat = "mp4",
                    Sha256Hash = "abc"
                };

                _ = _mockEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([evt]);
                _ = _mockDownloadEventRepo.Setup(r => r.GetByProviderEventIdAsync(deviceId, "evt-1", It.IsAny<CancellationToken>())).ReturnsAsync(downloadEvent);
                _ = _mockMediaItemRepo.Setup(r => r.GetByDownloadEventIdAsync(downloadEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync([mediaItem]);

                BackupExportOrchestrator orchestrator = CreateOrchestrator();
                PrepareExportResult result = await orchestrator.PrepareForExportAsync(CancellationToken.None);

                Assert.Equal(1, result.Validated);
                Assert.Equal(0, result.Backfilled);
                _mockTagger.Verify(t => t.TagEventIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task PrepareForExportAsync_MissingSidecar_BackfillsAndTagsFile()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                var deviceId = Guid.NewGuid();
                var evt = new Event { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", EventType = "motion", DownloadedAtUtc = DateTime.UtcNow };
                var downloadEvent = new DownloadEvent { Id = Guid.NewGuid(), DeviceId = deviceId, ProviderEventId = "evt-1", AppVersion = "1.0" };
                string mediaFilePath = Path.Combine(tempDir, "clip.mp4");
                File.WriteAllText(mediaFilePath, "dummy");

                var mediaItem = new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    DownloadEventId = downloadEvent.Id,
                    FileName = "clip.mp4",
                    FilePath = mediaFilePath,
                    MediaFormat = "mp4",
                    Sha256Hash = "abc"
                };

                _ = _mockEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([evt]);
                _ = _mockDownloadEventRepo.Setup(r => r.GetByProviderEventIdAsync(deviceId, "evt-1", It.IsAny<CancellationToken>())).ReturnsAsync(downloadEvent);
                _ = _mockMediaItemRepo.Setup(r => r.GetByDownloadEventIdAsync(downloadEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync([mediaItem]);
                _ = _mockTagger.Setup(t => t.TagEventIdAsync(mediaFilePath, evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

                BackupExportOrchestrator orchestrator = CreateOrchestrator();
                PrepareExportResult result = await orchestrator.PrepareForExportAsync(CancellationToken.None);

                Assert.Equal(1, result.Backfilled);
                Assert.True(File.Exists(Path.ChangeExtension(mediaFilePath, ".json")));
                _mockTagger.Verify(t => t.TagEventIdAsync(mediaFilePath, evt.Id, It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ExportBackupAsync_WithData_CreatesZipArchive()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                _ = _mockAccountRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockLocationRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockDeviceRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockDownloadEventRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockMediaItemRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
                _ = _mockConfig.Setup(c => c.DownloadLocation).Returns(tempDir);

                BackupExportOrchestrator orchestrator = CreateOrchestrator();
                BackupExportResult result = await orchestrator.ExportBackupAsync(tempDir, CancellationToken.None);

                Assert.True(result.Success);
                Assert.NotNull(result.ArchivePath);
                Assert.True(File.Exists(result.ArchivePath));
                Assert.EndsWith(".zip", result.ArchivePath);
                Assert.NotNull(result.ArchiveSha256Hash);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
