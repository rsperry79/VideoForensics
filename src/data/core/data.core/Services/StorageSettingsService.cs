using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Service for managing storage settings and relocation across different storage categories.
    /// Provides read access to current storage state and handles relocation of storage directories.
    /// </summary>
    internal class StorageSettingsService : IStorageSettingsService
    {
        private readonly IStorageLocationProvider _storageLocationProvider;
        private readonly IForensicsConfigurationService _configurationService;
        private readonly ILogger<StorageSettingsService> _logger;

        public StorageSettingsService(
            IStorageLocationProvider storageLocationProvider,
            IForensicsConfigurationService configurationService,
            ILogger<StorageSettingsService> logger)
        {
            _storageLocationProvider = storageLocationProvider ?? throw new ArgumentNullException(nameof(storageLocationProvider));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StorageSettings> GetStatusAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving storage settings status");

            // Load current configuration to get any overrides
            IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync("", cancellationToken);

            var categories = new List<StorageCategoryStatus>();

            foreach (StorageCategory category in Enum.GetValues(typeof(StorageCategory)))
            {
                string? configuredOverride = GetConfigurationOverride(config, category);
                string effectivePath = _storageLocationProvider.GetEffectiveRoot(category, configuredOverride);

                long usedBytes = ComputeDirectorySize(effectivePath);
                long freeBytes = ComputeFreeBytes(effectivePath);
                bool isRelocatable = category != StorageCategory.Keys;
                bool requiresRestart = category is StorageCategory.Database or StorageCategory.Logs;

                var status = new StorageCategoryStatus(
                    Category: category,
                    CurrentPath: effectivePath,
                    IsDefault: string.IsNullOrWhiteSpace(configuredOverride),
                    UsedBytes: usedBytes,
                    FreeBytes: freeBytes,
                    IsRelocatable: isRelocatable,
                    RequiresRestart: requiresRestart
                );

                categories.Add(status);
            }

            _logger.LogInformation("Storage settings status retrieved: {CategoryCount} categories", categories.Count);
            return new StorageSettings(categories);
        }

        public async Task<RelocateStorageCategoryResult> RelocateAsync(RelocateStorageCategoryRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Relocating storage category {Category} to {NewPath}", request.Category, request.NewRootPath);

            // Keys cannot be relocated
            if (request.Category == StorageCategory.Keys)
            {
                const string errorMessage = "Storage category 'Keys' cannot be relocated";
                _logger.LogError(errorMessage);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: errorMessage, RestartRequired: false);
            }

            // Validate the new path
            if (string.IsNullOrWhiteSpace(request.NewRootPath))
            {
                const string errorMessage = "New root path cannot be empty";
                _logger.LogError(errorMessage);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: errorMessage, RestartRequired: false);
            }

            try
            {
                // Ensure the destination directory exists
                Directory.CreateDirectory(request.NewRootPath);

                // Load current configuration to get the current path and override
                IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync("", cancellationToken);
                string? currentOverride = GetConfigurationOverride(config, request.Category);
                string currentPath = _storageLocationProvider.GetEffectiveRoot(request.Category, currentOverride);

                // Move files from current path to new path if current path exists
                if (Directory.Exists(currentPath) && currentPath != request.NewRootPath)
                {
                    try
                    {
                        await MoveDirectoryContentsAsync(currentPath, request.NewRootPath, cancellationToken);
                        _logger.LogInformation("Successfully moved {Category} from {OldPath} to {NewPath}", request.Category, currentPath, request.NewRootPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to move {Category} contents from {OldPath} to {NewPath}", request.Category, currentPath, request.NewRootPath);
                        return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to move storage files: {ex.Message}", RestartRequired: false);
                    }
                }

                // Persist the new override in configuration
                SetConfigurationOverride(config, request.Category, request.NewRootPath);

                try
                {
                    await _configurationService.SaveConfigurationAsync(config, cancellationToken);
                    _logger.LogInformation("Storage category {Category} configuration persisted to {NewPath}", request.Category, request.NewRootPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist storage configuration for {Category}", request.Category);
                    return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to save configuration: {ex.Message}", RestartRequired: false);
                }

                bool requiresRestart = request.Category is StorageCategory.Database or StorageCategory.Logs;
                _logger.LogInformation("Storage category {Category} relocation completed. Restart required: {RestartRequired}", request.Category, requiresRestart);
                return new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: requiresRestart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error relocating storage category {Category}", request.Category);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Unexpected error: {ex.Message}", RestartRequired: false);
            }
        }

        private string? GetConfigurationOverride(IForensicsConfiguration config, StorageCategory category)
        {
            return category switch
            {
                StorageCategory.Database => config.DatabaseLocation,
                StorageCategory.Media => config.DownloadLocation,
                StorageCategory.TempDownload => config.TempDownloadLocation,
                StorageCategory.Logs => config.LogsLocation,
                StorageCategory.Reports => config.ReportsLocation,
                StorageCategory.Backup => config.QueryExportLocation,
                StorageCategory.Keys => null, // Keys never has an override
                _ => null
            };
        }

        private void SetConfigurationOverride(IForensicsConfiguration config, StorageCategory category, string newPath)
        {
            switch (category)
            {
                case StorageCategory.Database:
                    config.DatabaseLocation = newPath;
                    break;
                case StorageCategory.Media:
                    config.DownloadLocation = newPath;
                    break;
                case StorageCategory.TempDownload:
                    config.TempDownloadLocation = newPath;
                    break;
                case StorageCategory.Logs:
                    config.LogsLocation = newPath;
                    break;
                case StorageCategory.Reports:
                    config.ReportsLocation = newPath;
                    break;
                case StorageCategory.Backup:
                    config.QueryExportLocation = newPath;
                    break;
                case StorageCategory.Keys:
                    // Keys cannot be relocated, no override set
                    break;
            }
        }

        private long ComputeDirectorySize(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return 0;
                }

                var directoryInfo = new DirectoryInfo(path);
                long size = 0;

                try
                {
                    foreach (FileInfo file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        size += file.Length;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning("Access denied computing size for {Path}", path);
                    return 0;
                }

                return size;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute directory size for {Path}", path);
                return 0;
            }
        }

        private long ComputeFreeBytes(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                {
                    return 0;
                }

                var driveInfo = new DriveInfo(root);
                return driveInfo.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute free space for {Path}", path);
                return 0;
            }
        }

        private async Task MoveDirectoryContentsAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            var sourceDir = new DirectoryInfo(sourcePath);
            var destDir = new DirectoryInfo(destinationPath);

            // Move all files
            foreach (FileInfo file in sourceDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(sourcePath, file.FullName);
                string destFilePath = Path.Combine(destinationPath, relativePath);

                // Ensure destination directory exists
                string? destFileDir = Path.GetDirectoryName(destFilePath);
                if (destFileDir != null)
                {
                    Directory.CreateDirectory(destFileDir);
                }

                // Copy file with overwrite
                File.Copy(file.FullName, destFilePath, overwrite: true);
                File.Delete(file.FullName);
            }

            // Remove empty source directories (from innermost to outermost)
            var emptyDirs = sourceDir.EnumerateDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length)
                .ToList();

            foreach (DirectoryInfo dir in emptyDirs)
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(dir.FullName).Count() == 0)
                    {
                        Directory.Delete(dir.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete empty directory {Path}", dir.FullName);
                }
            }
        }
    }
}
