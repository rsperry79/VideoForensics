using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>
    /// Tests for <see cref="MediaStorageMigrator"/>: moving media from the saved legacy DownloadLocation into the
    /// service media root as &lt;provider&gt;\&lt;location&gt;\&lt;device&gt;, rewriting DB paths, never overwriting, and removing
    /// legacy folders only once they are empty.
    /// </summary>
    public class MediaStorageMigratorTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-migrate-" + Guid.NewGuid().ToString("N"));
        private readonly string _legacy;
        private readonly string _target;
        private readonly Mock<IStorageLocationProvider> _storage = new();
        private readonly Mock<IForensicsConfigurationService> _config = new();
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<ILogger<MediaStorageMigrator>> _logger = new();
        private readonly List<MediaItem> _rows = new();
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();

        public MediaStorageMigratorTests()
        {
            _legacy = Path.Combine(_root, "legacy");
            _target = Path.Combine(_root, "media");
            _ = Directory.CreateDirectory(_legacy);

            _ = _storage.Setup(x => x.GetDefaultRoot(StorageCategory.Media)).Returns(_target);
            _ = _media.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _rows.ToList());
            _ = _media.Setup(x => x.UpdateAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _ = _devices.Setup(x => x.GetAsync(_deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = _deviceId, LocationId = _locationId, ProviderDeviceId = "1", Name = "Front Door", Type = "doorbell" });
            _ = _locations.Setup(x => x.GetAsync(_locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Location { Id = _locationId, ProviderLocationId = "L1", Name = "Arlington" });
            _ = _locations.Setup(x => x.ListProviderAccountsForLocationAsync(_locationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ProviderAccount> { new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring" } });
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private MediaStorageMigrator CreateMigrator() =>
            new(_storage.Object, _config.Object, _media.Object, _devices.Object, _locations.Object, _logger.Object);

        private void SetLegacyLocation(string? path)
        {
            var configuration = new ForensicsConfiguration { DownloadLocation = path };
            _ = _config.Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuration);
        }

        private MediaItem AddRow(string filePath)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = _deviceId,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                MediaFormat = "mp4",
                Sha256Hash = "hash",
            };
            _rows.Add(item);
            return item;
        }

        private static void WriteFile(string path, string content)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        [Fact]
        public async Task MovesFileToProviderLocationDeviceFolder()
        {
            string legacyFile = Path.Combine(_legacy, "acct", "clip.mp4");
            WriteFile(legacyFile, "video");
            _ = AddRow(legacyFile);
            SetLegacyLocation(_legacy);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            string expected = Path.Combine(_target, "ring", "arlington", "front_door", "clip.mp4");
            Assert.True(File.Exists(expected));
            Assert.False(File.Exists(legacyFile));
            Assert.Equal(1, result.FilesMoved);
        }

        [Fact]
        public async Task RewritesFilePathInDatabase()
        {
            string legacyFile = Path.Combine(_legacy, "acct", "clip.mp4");
            WriteFile(legacyFile, "video");
            MediaItem row = AddRow(legacyFile);
            SetLegacyLocation(_legacy);

            _ = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal(Path.Combine(_target, "ring", "arlington", "front_door", "clip.mp4"), row.FilePath);
            _media.Verify(x => x.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LeavesRowsOutsideLegacyRootAlone()
        {
            string elsewhere = Path.Combine(_root, "other", "clip.mp4");
            MediaItem row = AddRow(elsewhere);
            WriteFile(Path.Combine(_legacy, "acct", "clip.mp4"), "video");
            SetLegacyLocation(_legacy);

            _ = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal(elsewhere, row.FilePath);
            _media.Verify(x => x.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotOverwriteExistingDestinationFile()
        {
            string legacyFile = Path.Combine(_legacy, "acct", "clip.mp4");
            string destination = Path.Combine(_target, "ring", "arlington", "front_door", "clip.mp4");
            WriteFile(legacyFile, "legacy");
            WriteFile(destination, "already-there");
            _ = AddRow(legacyFile);
            SetLegacyLocation(_legacy);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal("already-there", File.ReadAllText(destination));
            Assert.True(File.Exists(legacyFile));
            Assert.Equal(1, result.FilesSkipped);
        }

        [Fact]
        public async Task KeepsLegacyFolderWhenAFileIsSkipped()
        {
            string movedFile = Path.Combine(_legacy, "acct", "moved.mp4");
            string skippedFile = Path.Combine(_legacy, "acct", "skipped.mp4");
            WriteFile(movedFile, "video");
            WriteFile(skippedFile, "video");
            WriteFile(Path.Combine(_target, "ring", "arlington", "front_door", "skipped.mp4"), "already-there");
            _ = AddRow(movedFile);
            _ = AddRow(skippedFile);
            SetLegacyLocation(_legacy);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.False(File.Exists(movedFile));
            Assert.True(File.Exists(skippedFile));
            Assert.True(Directory.Exists(_legacy));
            Assert.False(result.LegacyRootRemoved);
        }

        [Fact]
        public async Task RemovesEmptyLegacyRootAndClearsOverride()
        {
            WriteFile(Path.Combine(_legacy, "acct", "clip.mp4"), "video");
            _ = AddRow(Path.Combine(_legacy, "acct", "clip.mp4"));
            SetLegacyLocation(_legacy);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.False(Directory.Exists(_legacy));
            Assert.True(result.LegacyRootRemoved);
            _config.Verify(x => x.SaveConfigurationAsync(
                It.Is<IForensicsConfiguration>(c => c.DownloadLocation == null),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SkipsRowWhoseDeviceIsMissing()
        {
            string legacyFile = Path.Combine(_legacy, "acct", "orphan.mp4");
            WriteFile(legacyFile, "video");
            var orphan = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "orphan.mp4",
                FilePath = legacyFile,
                MediaFormat = "mp4",
                Sha256Hash = "hash",
            };
            _rows.Add(orphan);
            SetLegacyLocation(_legacy);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.True(File.Exists(legacyFile));
            Assert.Equal(legacyFile, orphan.FilePath);
            Assert.Equal(1, result.FilesSkipped);
        }

        [Fact]
        public async Task NoOpWhenNoLegacyLocationSaved()
        {
            SetLegacyLocation(null);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal(0, result.FilesMoved);
            _config.Verify(x => x.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoOpWhenLegacyEqualsTarget()
        {
            WriteFile(Path.Combine(_target, "acct", "clip.mp4"), "video");
            SetLegacyLocation(_target);

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal(0, result.FilesMoved);
            Assert.True(File.Exists(Path.Combine(_target, "acct", "clip.mp4")));
        }

        [Fact]
        public async Task NoOpWhenLegacyFolderMissing()
        {
            SetLegacyLocation(Path.Combine(_root, "does-not-exist"));

            MediaMigrationResult result = await CreateMigrator().MigrateAsync(CancellationToken.None);

            Assert.Equal(0, result.FilesMoved);
        }
    }
}
