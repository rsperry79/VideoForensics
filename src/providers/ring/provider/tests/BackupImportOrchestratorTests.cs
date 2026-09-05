using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Extensions.Logging;

using Moq;

using System.Text;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    internal class FakeUnitOfWorkContext : IUnitOfWorkContext
    {
        public IUserRepository Users { get; set; } = null!;
        public IProviderAccountRepository ProviderAccounts { get; set; } = null!;
        public ILocationRepository Locations { get; set; } = null!;
        public IDeviceRepository Devices { get; set; } = null!;
        public IMediaItemRepository MediaItems { get; set; } = null!;
        public IDownloadEventRepository DownloadEvents { get; set; } = null!;
        public ICredentialRepository Credentials { get; set; } = null!;
        public IActionLogRepository ActionLog { get; set; } = null!;
        public IEventRepository Events { get; set; } = null!;
        public IDeviceConfigRepository DeviceConfig { get; set; } = null!;
        public IAnnotationRepository Annotations { get; set; } = null!;
        public IProviderReconciliationRepository ProviderReconciliation { get; set; } = null!;
        public IExportRecordRepository ExportRecords { get; set; } = null!;
    }

    public class BackupImportOrchestratorTests
    {
        private readonly Mock<ILogger<BackupImportOrchestrator>> _mockLogger = new();
        private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
        private readonly Mock<IUserRepository> _mockUsers = new();
        private readonly Mock<IProviderAccountRepository> _mockAccounts = new();
        private readonly Mock<ILocationRepository> _mockLocations = new();
        private readonly Mock<IDeviceRepository> _mockDevices = new();
        private readonly Mock<IEventRepository> _mockEvents = new();
        private readonly Mock<IDownloadEventRepository> _mockDownloadEvents = new();
        private readonly Mock<IMediaItemRepository> _mockMediaItems = new();

        private static string CreateTempDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private void WireUnitOfWork()
        {
            var ctx = new FakeUnitOfWorkContext
            {
                Users = _mockUsers.Object,
                ProviderAccounts = _mockAccounts.Object,
                Locations = _mockLocations.Object,
                Devices = _mockDevices.Object,
                Events = _mockEvents.Object,
                DownloadEvents = _mockDownloadEvents.Object,
                MediaItems = _mockMediaItems.Object
            };

            _ = _mockUnitOfWork
                .Setup(u => u.ExecuteAsync(It.IsAny<Func<IUnitOfWorkContext, Task<BackupImportResult>>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<IUnitOfWorkContext, Task<BackupImportResult>> work, CancellationToken _) => work(ctx));
        }

        private static string CreateBackupZip(
            string zipPath,
            List<ProviderAccount>? accounts = null,
            List<Location>? locations = null,
            List<Device>? devices = null,
            List<Event>? events = null,
            List<DownloadEvent>? downloadEvents = null,
            List<ExportedMediaItem>? mediaItems = null)
        {
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            using var zipStream = new ZipOutputStream(File.Create(zipPath));

            void WriteEntry(string name, string json)
            {
                var entry = new ZipEntry(name);
                zipStream.PutNextEntry(entry);
                using var writer = new StreamWriter(zipStream, Encoding.UTF8, leaveOpen: true);
                writer.Write(json);
                writer.Flush();
                zipStream.CloseEntry();
            }

            WriteEntry("accounts.json", JsonSerializer.Serialize(accounts ?? new List<ProviderAccount>(), jsonOptions));
            WriteEntry("locations.json", JsonSerializer.Serialize(locations ?? new List<Location>(), jsonOptions));
            WriteEntry("devices.json", JsonSerializer.Serialize(devices ?? new List<Device>(), jsonOptions));
            WriteEntry("events.json", JsonSerializer.Serialize(events ?? new List<Event>(), jsonOptions));
            WriteEntry("download_events.json", JsonSerializer.Serialize(downloadEvents ?? new List<DownloadEvent>(), jsonOptions));
            WriteEntry("media_items.json", JsonSerializer.Serialize(mediaItems ?? new List<ExportedMediaItem>(), jsonOptions));

            return zipPath;
        }

        [Fact]
        public async Task ImportBackupAsync_SkipsExistingProviderAccount()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "ring" };
                var zipPath = CreateBackupZip(Path.Combine(tempDir, "backup.zip"), accounts: new List<ProviderAccount> { account });

                _ = _mockAccounts.Setup(r => r.GetAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);
                WireUnitOfWork();

                var orchestrator = new BackupImportOrchestrator(_mockLogger.Object, _mockUnitOfWork.Object);
                var result = await orchestrator.ImportBackupAsync(zipPath, tempDir, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(1, result.ProviderAccounts.SkippedExisting);
                Assert.Equal(0, result.ProviderAccounts.Inserted);
                _mockAccounts.Verify(r => r.AddAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ImportBackupAsync_SkipsOrphanedLocation_WhenAccountMissing()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                var location = new Location { Id = Guid.NewGuid(), ProviderAccountId = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
                var zipPath = CreateBackupZip(Path.Combine(tempDir, "backup.zip"), locations: new List<Location> { location });

                _ = _mockLocations.Setup(r => r.GetAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);
                _ = _mockAccounts.Setup(r => r.GetAsync(location.ProviderAccountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);
                WireUnitOfWork();

                var orchestrator = new BackupImportOrchestrator(_mockLogger.Object, _mockUnitOfWork.Object);
                var result = await orchestrator.ImportBackupAsync(zipPath, tempDir, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(1, result.Locations.SkippedOrphaned);
                Assert.Equal(0, result.Locations.Inserted);
                _mockLocations.Verify(r => r.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task ImportBackupAsync_MediaItem_FlagsIntegrityIssue_WhenFileMissing()
        {
            string tempDir = CreateTempDirectory();
            try
            {
                var deviceId = Guid.NewGuid();
                var exportedItem = new ExportedMediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    DownloadEventId = null,
                    FileName = "missing.mp4",
                    RelativeMediaPath = "missing.mp4",
                    MediaFormat = "mp4",
                    Sha256Hash = "abc123"
                };
                var zipPath = CreateBackupZip(Path.Combine(tempDir, "backup.zip"), mediaItems: new List<ExportedMediaItem> { exportedItem });

                _ = _mockMediaItems.Setup(r => r.GetAsync(exportedItem.Id, It.IsAny<CancellationToken>())).ReturnsAsync((MediaItem?)null);
                _ = _mockDevices.Setup(r => r.GetAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new Device { Id = deviceId, LocationId = Guid.NewGuid(), ProviderDeviceId = "d1", Name = "Cam", Type = "camera" });
                WireUnitOfWork();

                var orchestrator = new BackupImportOrchestrator(_mockLogger.Object, _mockUnitOfWork.Object);
                var result = await orchestrator.ImportBackupAsync(zipPath, tempDir, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(1, result.MediaItems.Inserted);
                Assert.Equal(1, result.MediaItems.IntegrityIssues);
                _mockMediaItems.Verify(r => r.AddAsync(It.Is<MediaItem>(m => m.Id == exportedItem.Id && !m.IntegrityVerified), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
