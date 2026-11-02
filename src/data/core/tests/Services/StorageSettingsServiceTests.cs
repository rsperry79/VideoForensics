using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class StorageSettingsServiceTests
    {
        private readonly Mock<IStorageLocationProvider> _mockStorageLocationProvider;
        private readonly Mock<IForensicsConfigurationService> _mockConfigurationService;
        private readonly Mock<ILogger<StorageSettingsService>> _mockLogger;
        private readonly Mock<IDatabaseMaintenanceService> _mockMaintenance;
        private readonly StorageSettingsService _service;

        public StorageSettingsServiceTests()
        {
            _mockStorageLocationProvider = new Mock<IStorageLocationProvider>();
            _mockConfigurationService = new Mock<IForensicsConfigurationService>();
            _mockLogger = new Mock<ILogger<StorageSettingsService>>();

            // Default mocks return temp path for all categories
            _ = _mockStorageLocationProvider
                .Setup(x => x.GetDefaultRoot(It.IsAny<StorageCategory>()))
                .Returns((StorageCategory cat) => Path.Combine(Path.GetTempPath(), cat.ToString()));

            _ = _mockStorageLocationProvider
                .Setup(x => x.GetEffectiveRoot(It.IsAny<StorageCategory>(), It.IsAny<string?>()))
                .Returns((StorageCategory cat, string? overridePath) =>
                    string.IsNullOrWhiteSpace(overridePath)
                        ? Path.Combine(Path.GetTempPath(), cat.ToString())
                        : overridePath);

            // Keep bootstrap pointer writes out of the real ProgramData folder
            _ = _mockStorageLocationProvider
                .Setup(x => x.GetDataRoot())
                .Returns(Path.Combine(Path.GetTempPath(), "vf-data-" + Guid.NewGuid().ToString("N")));

            // Default mock for configuration loads empty config (no overrides)
            _ = _mockConfigurationService
                .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ForensicsConfiguration());

            _mockMaintenance = new Mock<IDatabaseMaintenanceService>();

            _service = new StorageSettingsService(
                _mockStorageLocationProvider.Object,
                _mockConfigurationService.Object,
                _mockMaintenance.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetStatusAsync_IncludesTheDefaultStorageRoot()
        {
            _ = _mockStorageLocationProvider
                .Setup(x => x.GetDefaultStorageRoot())
                .Returns(@"D:\VideoForensics");

            StorageSettings status = await _service.GetStatusAsync(CancellationToken.None);

            Assert.Equal(@"D:\VideoForensics", status.DefaultRoot);
        }

        [Fact]
        public async Task RelocateAsync_NullPath_ResetsToPlatformDefaultAndClearsOverride()
        {
            // Arrange: Media is on a custom folder that does not exist on disk, so nothing needs to move
            const string customPath = @"D:\Custom\Media";
            string platformDefault = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var config = new ForensicsConfiguration { DownloadLocation = customPath };

            _ = _mockConfigurationService
                .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(config);

            _ = _mockStorageLocationProvider
                .Setup(x => x.GetPlatformDefaultRoot(StorageCategory.Media))
                .Returns(platformDefault);

            var request = new RelocateStorageCategoryRequest(Category: StorageCategory.Media, NewRootPath: null);

            // Act
            RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

            // Assert: the override is cleared, so the category reads as Default again
            Assert.True(result.Succeeded);
            Assert.Null(config.DownloadLocation);
            _mockConfigurationService.Verify(x => x.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RelocateAsync_DatabaseMove_ReleasesConnectionsThenMovesFile()
        {
            // Arrange: a real source folder holding a database file, so the move has something to do
            string sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "videoforensics.db"), "db");

            try
            {
                _ = _mockStorageLocationProvider
                    .Setup(x => x.GetEffectiveRoot(StorageCategory.Database, It.IsAny<string?>()))
                    .Returns(sourceDir);

                _mockMaintenance
                    .Setup(x => x.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>()))
                    .Callback(() => Assert.True(File.Exists(Path.Combine(sourceDir, "videoforensics.db")), "file must still be in place when connections are released"))
                    .Returns(Task.CompletedTask);

                var request = new RelocateStorageCategoryRequest(Category: StorageCategory.Database, NewRootPath: destDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                _mockMaintenance.Verify(x => x.ReleaseAllConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
                Assert.True(File.Exists(Path.Combine(destDir, "videoforensics.db")));
                Assert.False(File.Exists(Path.Combine(sourceDir, "videoforensics.db")));
            }
            finally
            {
                foreach (string dir in new[] { sourceDir, destDir })
                {
                    try
                    { Directory.Delete(dir, recursive: true); }
                    catch { }
                }
            }
        }

        [Fact]
        public async Task GetStatusAsync_ReturnsAllEightCategories()
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(settings);
            Assert.NotNull(settings.Categories);
            Assert.Equal(8, settings.Categories.Count);

            // Verify all categories are present
            var categorySet = new HashSet<StorageCategory>(settings.Categories.Select(c => c.Category));
            Assert.Contains(StorageCategory.Database, categorySet);
            Assert.Contains(StorageCategory.Media, categorySet);
            Assert.Contains(StorageCategory.TempDownload, categorySet);
            Assert.Contains(StorageCategory.Logs, categorySet);
            Assert.Contains(StorageCategory.Reports, categorySet);
            Assert.Contains(StorageCategory.Backup, categorySet);
            Assert.Contains(StorageCategory.Exports, categorySet);
            Assert.Contains(StorageCategory.Keys, categorySet);
        }

        [Fact]
        public async Task GetStatusAsync_IncludesExports_RelocatableWithoutRestart()
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            StorageCategoryStatus? exportsStatus = settings.Categories.FirstOrDefault(c => c.Category == StorageCategory.Exports);
            Assert.NotNull(exportsStatus);
            Assert.True(exportsStatus.IsRelocatable);
            Assert.False(exportsStatus.RequiresRestart);
        }

        [Fact]
        public async Task RelocateAsync_ExportsCategory_PersistsExportsLocation()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var config = new ForensicsConfiguration();
                _ = _mockConfigurationService
                    .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(config);

                var request = new RelocateStorageCategoryRequest(
                    Category: StorageCategory.Exports,
                    NewRootPath: tempDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                Assert.False(result.RestartRequired);
                Assert.Equal(tempDir, config.ExportsLocation);
            }
            finally
            {
                try
                { Directory.Delete(tempDir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public async Task GetStatusAsync_KeysIsNotRelocatable()
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            StorageCategoryStatus? keysStatus = settings.Categories.FirstOrDefault(c => c.Category == StorageCategory.Keys);
            Assert.NotNull(keysStatus);
            Assert.False(keysStatus.IsRelocatable);
        }

        [Theory]
        [InlineData(StorageCategory.Database)]
        [InlineData(StorageCategory.Logs)]
        public async Task GetStatusAsync_DatabaseAndLogsRequireRestart(StorageCategory category)
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            StorageCategoryStatus? status = settings.Categories.FirstOrDefault(c => c.Category == category);
            Assert.NotNull(status);
            Assert.True(status.RequiresRestart);
        }

        [Theory]
        [InlineData(StorageCategory.Media)]
        [InlineData(StorageCategory.TempDownload)]
        [InlineData(StorageCategory.Reports)]
        [InlineData(StorageCategory.Backup)]
        [InlineData(StorageCategory.Exports)]
        public async Task GetStatusAsync_OtherCategoriesDoNotRequireRestart(StorageCategory category)
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            StorageCategoryStatus? status = settings.Categories.FirstOrDefault(c => c.Category == category);
            Assert.NotNull(status);
            Assert.False(status.RequiresRestart);
        }

        [Fact]
        public async Task GetStatusAsync_AllCategoriesAreRelocatableExceptKeys()
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            foreach (StorageCategoryStatus status in settings.Categories)
            {
                if (status.Category == StorageCategory.Keys)
                {
                    Assert.False(status.IsRelocatable);
                }
                else
                {
                    Assert.True(status.IsRelocatable, $"Category {status.Category} should be relocatable");
                }
            }
        }

        [Fact]
        public async Task RelocateAsync_RejectsKeysCategory()
        {
            // Arrange
            var request = new RelocateStorageCategoryRequest(
                Category: StorageCategory.Keys,
                NewRootPath: "/some/path");

            // Act
            RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("Keys", result.ErrorMessage);
            Assert.False(result.RestartRequired);
        }

        [Fact]
        public async Task RelocateAsync_RejectsEmptyPath()
        {
            // Arrange
            var request = new RelocateStorageCategoryRequest(
                Category: StorageCategory.Reports,
                NewRootPath: string.Empty);

            // Act
            RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task RelocateAsync_SucceedsAndPersistsConfigForRelocatableCategory()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var config = new ForensicsConfiguration();
                _ = _mockConfigurationService
                    .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(config);

                _ = _mockStorageLocationProvider
                    .Setup(x => x.GetEffectiveRoot(StorageCategory.Reports, It.IsAny<string?>()))
                    .Returns((StorageCategory cat, string? overridePath) =>
                        string.IsNullOrWhiteSpace(overridePath)
                            ? Path.Combine(Path.GetTempPath(), cat.ToString())
                            : overridePath);

                var request = new RelocateStorageCategoryRequest(
                    Category: StorageCategory.Reports,
                    NewRootPath: tempDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                Assert.Null(result.ErrorMessage);
                Assert.False(result.RestartRequired); // Reports doesn't require restart

                // Verify configuration was saved
                _mockConfigurationService.Verify(
                    x => x.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()),
                    Times.Once);

                // Verify the config was updated with new path
                Assert.Equal(tempDir, config.ReportsLocation);
            }
            finally
            {
                try
                { Directory.Delete(tempDir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public async Task RelocateAsync_DatabaseMove_WritesBootstrapPointer()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                _ = _mockStorageLocationProvider
                    .Setup(x => x.GetDataRoot())
                    .Returns(dataRoot);

                var request = new RelocateStorageCategoryRequest(Category: StorageCategory.Database, NewRootPath: tempDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                Assert.Equal(tempDir, DatabaseLocationPointer.Read(dataRoot));
            }
            finally
            {
                foreach (string dir in new[] { tempDir, dataRoot })
                {
                    try
                    { Directory.Delete(dir, recursive: true); }
                    catch { }
                }
            }
        }

        [Theory]
        [InlineData(StorageCategory.Database)]
        [InlineData(StorageCategory.Logs)]
        public async Task RelocateAsync_RequiresRestartForDatabaseAndLogs(StorageCategory category)
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);

            try
            {
                var config = new ForensicsConfiguration();
                _ = _mockConfigurationService
                    .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(config);

                _ = _mockStorageLocationProvider
                    .Setup(x => x.GetEffectiveRoot(category, It.IsAny<string?>()))
                    .Returns((StorageCategory cat, string? overridePath) =>
                        string.IsNullOrWhiteSpace(overridePath)
                            ? Path.Combine(Path.GetTempPath(), cat.ToString())
                            : overridePath);

                var request = new RelocateStorageCategoryRequest(Category: category, NewRootPath: tempDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                Assert.True(result.RestartRequired);
            }
            finally
            {
                try
                { Directory.Delete(tempDir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public async Task RelocateAsync_CreatesDestinationDirectoryIfNotExists()
        {
            // Arrange
            string parentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string newDir = Path.Combine(parentDir, "new-storage");
            Assert.False(Directory.Exists(newDir));

            try
            {
                var config = new ForensicsConfiguration();
                _ = _mockConfigurationService
                    .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(config);

                _ = _mockStorageLocationProvider
                    .Setup(x => x.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>()))
                    .Returns((StorageCategory cat, string? overridePath) =>
                        string.IsNullOrWhiteSpace(overridePath)
                            ? Path.Combine(Path.GetTempPath(), cat.ToString())
                            : overridePath);

                var request = new RelocateStorageCategoryRequest(
                    Category: StorageCategory.Media,
                    NewRootPath: newDir);

                // Act
                RelocateStorageCategoryResult result = await _service.RelocateAsync(request, CancellationToken.None);

                // Assert
                Assert.True(result.Succeeded);
                Assert.True(Directory.Exists(newDir));
            }
            finally
            {
                try
                { Directory.Delete(parentDir, recursive: true); }
                catch { }
            }
        }
    }
}
