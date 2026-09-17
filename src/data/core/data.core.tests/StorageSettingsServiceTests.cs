using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Common.Helpers.Platform;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class StorageSettingsServiceTests
    {
        private readonly Mock<IStorageLocationProvider> _mockStorageLocationProvider;
        private readonly Mock<IForensicsConfigurationService> _mockConfigurationService;
        private readonly Mock<ILogger<StorageSettingsService>> _mockLogger;
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

            // Default mock for configuration loads empty config (no overrides)
            _ = _mockConfigurationService
                .Setup(x => x.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ForensicsConfiguration());

            _service = new StorageSettingsService(
                _mockStorageLocationProvider.Object,
                _mockConfigurationService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetStatusAsync_ReturnsAllSevenCategories()
        {
            // Act
            StorageSettings settings = await _service.GetStatusAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(settings);
            Assert.NotNull(settings.Categories);
            Assert.Equal(7, settings.Categories.Count);

            // Verify all categories are present
            var categorySet = new HashSet<StorageCategory>(settings.Categories.Select(c => c.Category));
            Assert.Contains(StorageCategory.Database, categorySet);
            Assert.Contains(StorageCategory.Media, categorySet);
            Assert.Contains(StorageCategory.TempDownload, categorySet);
            Assert.Contains(StorageCategory.Logs, categorySet);
            Assert.Contains(StorageCategory.Reports, categorySet);
            Assert.Contains(StorageCategory.Backup, categorySet);
            Assert.Contains(StorageCategory.Keys, categorySet);
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
                NewRootPath: "");

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
