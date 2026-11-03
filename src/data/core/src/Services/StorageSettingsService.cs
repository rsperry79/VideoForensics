using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;

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
        private readonly IDatabaseMaintenanceService _maintenance;
        private readonly ILogger<StorageSettingsService> _logger;

        public StorageSettingsService(
            IStorageLocationProvider storageLocationProvider,
            IForensicsConfigurationService configurationService,
            IDatabaseMaintenanceService maintenance,
            ILogger<StorageSettingsService> logger)
        {
            _storageLocationProvider = storageLocationProvider ?? throw new ArgumentNullException(nameof(storageLocationProvider));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StorageSettings> GetStatusAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving storage settings status");

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
                    // Keys never follow the default root, so they are never reported as on the default location.
                    IsDefault: category != StorageCategory.Keys && string.IsNullOrWhiteSpace(configuredOverride),
                    UsedBytes: usedBytes,
                    FreeBytes: freeBytes,
                    IsRelocatable: isRelocatable,
                    RequiresRestart: requiresRestart
                );

                categories.Add(status);
            }

            _logger.LogDebug("Storage settings status retrieved: {CategoryCount} categories", categories.Count);
            return new StorageSettings(categories, _storageLocationProvider.GetDefaultStorageRoot());
        }

        public async Task<RelocateStorageCategoryResult> RelocateAsync(RelocateStorageCategoryRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Relocating storage category {Category} to {NewPath}", request.Category, request.NewRootPath ?? "platform default");

            // Keys cannot be relocated
            if (request.Category == StorageCategory.Keys)
            {
                const string errorMessage = "Storage category 'Keys' cannot be relocated";
                _logger.LogError(errorMessage);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: errorMessage, RestartRequired: false);
            }

            // A null path resets the category to its platform default. An empty path is rejected.
            bool reset = request.NewRootPath is null;
            if (!reset && string.IsNullOrWhiteSpace(request.NewRootPath))
            {
                const string errorMessage = "New root path cannot be empty";
                _logger.LogError(errorMessage);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: errorMessage, RestartRequired: false);
            }

            string newRootPath = reset ? _storageLocationProvider.GetPlatformDefaultRoot(request.Category) : request.NewRootPath!;

            try
            {
                // Ensure the destination directory exists
                _ = Directory.CreateDirectory(newRootPath);

                // Load current configuration to get the current path and override
                IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync("", cancellationToken);
                string? currentOverride = GetConfigurationOverride(config, request.Category);
                string currentPath = _storageLocationProvider.GetEffectiveRoot(request.Category, currentOverride);

                bool isDatabase = request.Category == StorageCategory.Database;
                bool moveNeeded = Directory.Exists(currentPath) && currentPath != newRootPath;

                if (isDatabase)
                {
                    // Saved while the database is still at its current path, so the file that moves carries the new override.
                    SetConfigurationOverride(config, request.Category, reset ? null : newRootPath);
                    RelocateStorageCategoryResult? saveFailure = await TrySaveConfigurationAsync(config, request.Category, cancellationToken);
                    if (saveFailure is not null)
                    {
                        return saveFailure;
                    }

                    if (moveNeeded)
                    {
                        // Pooled connections keep the file handle open, so release them before the file moves.
                        await _maintenance.ReleaseAllConnectionsAsync(cancellationToken);
                    }
                }

                // Move files from current path to new path if current path exists
                if (moveNeeded)
                {
                    try
                    {
                        if (request.Category == StorageCategory.Logs)
                        {
                            // The running server holds its audit file open, so the originals stay until the restart removes them.
                            CopyLogsForRestart(currentPath, newRootPath);
                        }
                        else
                        {
                            await MoveDirectoryContentsAsync(currentPath, newRootPath, cancellationToken);
                        }

                        _logger.LogDebug("Successfully moved {Category} from {OldPath} to {NewPath}", request.Category, currentPath, newRootPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to move {Category} contents from {OldPath} to {NewPath}: {ExceptionType} {ErrorMessage}", request.Category, currentPath, newRootPath, ex.GetType().Name, ex.Message);
                        if (request.Category == StorageCategory.Logs)
                        {
                            // The originals are complete, so only the partial copies go.
                            LogRelocationManifest.Discard(newRootPath);
                        }

                        if (isDatabase)
                        {
                            // The file did not move, so the saved override must point back at it.
                            SetConfigurationOverride(config, request.Category, currentOverride);
                            _ = await TrySaveConfigurationAsync(config, request.Category, cancellationToken);
                        }

                        return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to move storage files: {ex.Message}", RestartRequired: false);
                    }
                }

                if (isDatabase)
                {
                    // The saved override lives inside the database, which startup cannot open to read it. Record a
                    // Database move in the bootstrap pointer that every startup path reads before opening the database.
                    try
                    {
                        DatabaseLocationPointer.Write(_storageLocationProvider.GetDataRoot(), newRootPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to write Database location pointer to {NewPath}: {ExceptionType} {ErrorMessage}", newRootPath, ex.GetType().Name, ex.Message);
                        return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to record new database location: {ex.Message}", RestartRequired: false);
                    }
                }
                else
                {
                    // Persist the new override in configuration
                    SetConfigurationOverride(config, request.Category, reset ? null : newRootPath);
                    RelocateStorageCategoryResult? saveFailure = await TrySaveConfigurationAsync(config, request.Category, cancellationToken);
                    if (saveFailure is not null)
                    {
                        return saveFailure;
                    }

                    if (request.Category == StorageCategory.Logs)
                    {
                        // Startup reads the Logs folder from this pointer, because the logger starts before the saved configuration can be read.
                        if (reset)
                        {
                            LogsLocationPointer.Clear(_storageLocationProvider.GetDataRoot());
                        }
                        else
                        {
                            LogsLocationPointer.Write(_storageLocationProvider.GetDataRoot(), newRootPath);
                        }
                    }
                }

                bool requiresRestart = request.Category is StorageCategory.Database or StorageCategory.Logs;
                _logger.LogInformation("Storage category {Category} relocation completed. Restart required: {RestartRequired}", request.Category, requiresRestart);
                return new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: requiresRestart);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error relocating storage category {Category}: {Message} ({ErrorType})", request.Category, ex.Message, ex.GetType().Name);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Unexpected error: {ex.Message}", RestartRequired: false);
            }
        }

        /// <inheritdoc />
        public async Task<RelocateStorageCategoryResult> ChangeDefaultRootAsync(string newDefaultRoot, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(newDefaultRoot))
            {
                const string errorMessage = "New default root cannot be empty";
                _logger.LogError(errorMessage);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: errorMessage, RestartRequired: false);
            }

            string oldRoot = Path.GetFullPath(_storageLocationProvider.GetDefaultStorageRoot());
            string newRoot = Path.GetFullPath(newDefaultRoot);
            if (string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: false);
            }

            try
            {
                IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync("", cancellationToken);

                // Only categories sitting on the default root move. An in-app override, an install-time override, or a
                // relocated Database is not under the root, so it is skipped. Keys never move.
                var plan = new List<(StorageCategory Category, string OldPath, string NewPath)>();
                var clearedOverrides = new List<StorageCategory>();
                foreach (StorageCategory category in Enum.GetValues<StorageCategory>())
                {
                    if (category == StorageCategory.Keys)
                    {
                        continue;
                    }

                    // An override under the old root is part of the default-root move. Database and Logs keep their
                    // overrides because their bootstrap pointers would also need clearing. Those overrides are skipped.
                    string? overridePath = GetConfigurationOverride(config, category);
                    bool overrideUnderRoot = !string.IsNullOrWhiteSpace(overridePath) && IsUnderRoot(Path.GetFullPath(overridePath), oldRoot);
                    if (!string.IsNullOrWhiteSpace(overridePath) && !overrideUnderRoot)
                    {
                        continue;
                    }

                    if (category is StorageCategory.Database or StorageCategory.Logs && !string.IsNullOrWhiteSpace(overridePath))
                    {
                        continue;
                    }

                    string currentPath = Path.GetFullPath(overrideUnderRoot ? overridePath! : _storageLocationProvider.GetDefaultRoot(category));
                    if (!IsUnderRoot(currentPath, oldRoot))
                    {
                        continue;
                    }

                    plan.Add((category, currentPath, Path.Combine(newRoot, Path.GetRelativePath(oldRoot, currentPath))));
                    if (overrideUnderRoot)
                    {
                        clearedOverrides.Add(category);
                    }
                }

                _ = Directory.CreateDirectory(newRoot);
                if (plan.Any(p => p.Category == StorageCategory.Database))
                {
                    // Pooled connections keep the database file open, so release them before anything moves.
                    await _maintenance.ReleaseAllConnectionsAsync(cancellationToken);
                }

                var moved = new List<(string OldPath, string NewPath)>();
                var copiedLogs = new List<string>();
                try
                {
                    foreach ((StorageCategory category, string oldPath, string targetPath) in plan)
                    {
                        if (!Directory.Exists(oldPath))
                        {
                            continue;
                        }

                        if (category == StorageCategory.Logs)
                        {
                            // The running server holds its audit file open, so the originals stay until the restart removes them.
                            CopyLogsForRestart(oldPath, targetPath);
                            copiedLogs.Add(targetPath);
                            continue;
                        }

                        await MoveDirectoryContentsAsync(oldPath, targetPath, cancellationToken);
                        moved.Add((oldPath, targetPath));
                        _logger.LogDebug("Moved {Category} to the new default root", category);
                    }

                    // Overrides that pointed under the old root now follow the new default. Saved before the default is recorded.
                    if (clearedOverrides.Count > 0)
                    {
                        foreach (StorageCategory category in clearedOverrides)
                        {
                            SetConfigurationOverride(config, category, null);
                        }

                        RelocateStorageCategoryResult? saveFailure = await TrySaveConfigurationAsync(config, clearedOverrides[0], cancellationToken);
                        if (saveFailure is not null)
                        {
                            throw new InvalidOperationException(saveFailure.ErrorMessage);
                        }
                    }

                    // Recorded last, so a failed move leaves the old root in place for the next start.
                    DefaultStorageRootPointer.Write(_storageLocationProvider.GetDataRoot(), newRoot);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to change the default storage root to {NewRoot}: {ExceptionType} {ErrorMessage}", newRoot, ex.GetType().Name, ex.Message);
                    foreach (string copiedLogDir in copiedLogs)
                    {
                        LogRelocationManifest.Discard(copiedLogDir);
                    }

                    await MoveBackAsync(moved, cancellationToken);
                    return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to move storage files: {ex.Message}", RestartRequired: false);
                }

                // Every process reads the default root and the database location at startup, so the app restarts after a change.
                _logger.LogInformation("Default storage root changed to {NewRoot}. Categories moved: {MovedCount}", newRoot, moved.Count);
                return new RelocateStorageCategoryResult(Succeeded: true, ErrorMessage: null, RestartRequired: true);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error changing the default storage root: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Unexpected error: {ex.Message}", RestartRequired: false);
            }
        }

        private async Task MoveBackAsync(List<(string OldPath, string NewPath)> moved, CancellationToken cancellationToken)
        {
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                try
                {
                    await MoveDirectoryContentsAsync(moved[i].NewPath, moved[i].OldPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not move files back to {OldPath}: {ExceptionType} {ErrorMessage}", moved[i].OldPath, ex.GetType().Name, ex.Message);
                }
            }
        }

        private static bool IsUnderRoot(string path, string root)
        {
            string withSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            return path.StartsWith(withSeparator, StringComparison.OrdinalIgnoreCase);
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
                StorageCategory.Exports => config.ExportsLocation,
                StorageCategory.Keys => null, // Keys never has an override
                _ => null
            };
        }

        private async Task<RelocateStorageCategoryResult?> TrySaveConfigurationAsync(IForensicsConfiguration config, StorageCategory category, CancellationToken cancellationToken)
        {
            try
            {
                await _configurationService.SaveConfigurationAsync(config, cancellationToken);
                _logger.LogDebug("Storage category {Category} configuration persisted", category);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to persist storage configuration for {Category}: {ExceptionType} {ErrorMessage}", category, ex.GetType().Name, ex.Message);
                return new RelocateStorageCategoryResult(Succeeded: false, ErrorMessage: $"Failed to save configuration: {ex.Message}", RestartRequired: false);
            }
        }

        private void SetConfigurationOverride(IForensicsConfiguration config, StorageCategory category, string? newPath)
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
                case StorageCategory.Exports:
                    config.ExportsLocation = newPath;
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
                _logger.LogError("Failed to compute directory size for {Path}: {Message} ({ErrorType})", path, ex.Message, ex.GetType().Name);
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
                _logger.LogError("Failed to compute free space for {Path}: {Message} ({ErrorType})", path, ex.Message, ex.GetType().Name);
                return 0;
            }
        }

        /// <summary>
        /// Copies every log file into the new folder and records each copy's length, leaving the originals in place. The
        /// running server still holds its audit file open, so the originals are removed after the restart by
        /// <see cref="LogRelocationManifest.Reconcile"/>.
        /// </summary>
        private void CopyLogsForRestart(string sourcePath, string destinationPath)
        {
            var entries = new List<LogRelocationManifest.Entry>();
            foreach (FileInfo file in new DirectoryInfo(sourcePath).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourcePath, file.FullName);
                string destFilePath = Path.Combine(destinationPath, relativePath);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
                File.Copy(file.FullName, destFilePath, overwrite: true);

                // The copy's own length, not the original's, so any bytes the open file gains later are appended on restart.
                entries.Add(new LogRelocationManifest.Entry(file.FullName, relativePath, new FileInfo(destFilePath).Length));
            }

            LogRelocationManifest.Write(destinationPath, entries);
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
                    _ = Directory.CreateDirectory(destFileDir);
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
                    _logger.LogError("Failed to delete empty directory {Path}: {Message} ({ErrorType})", dir.FullName, ex.Message, ex.GetType().Name);
                }
            }
        }
    }
}
