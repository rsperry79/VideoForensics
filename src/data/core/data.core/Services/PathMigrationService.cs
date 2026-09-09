using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Migrates media from legacy user directories to ProgramData for system service compatibility.</summary>
    public class PathMigrationService : IPathMigrationService
    {
        private readonly ILogger<PathMigrationService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediaItemRepository _mediaItemRepository;

        public PathMigrationService(ILogger<PathMigrationService> logger, IUnitOfWork unitOfWork, IMediaItemRepository mediaItemRepository)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mediaItemRepository = mediaItemRepository;
        }

        public async Task MigrateToNewStorageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting path migration to ProgramData storage");

                var newMediaRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics",
                    "media");

                Directory.CreateDirectory(newMediaRoot);

                // Get all media items with paths to migrate
                var mediaItems = await _mediaItemRepository.ListAsync(cancellationToken);
                var itemsToMigrate = mediaItems.Where(m => !string.IsNullOrEmpty(m.FilePath) && IsLegacyPath(m.FilePath)).ToList();

                if (itemsToMigrate.Count == 0)
                {
                    _logger.LogInformation("No legacy media paths found, migration complete");
                    return;
                }

                _logger.LogInformation("Found {Count} media items with legacy paths to migrate", itemsToMigrate.Count);

                int successCount = 0;
                int failureCount = 0;

                foreach (var mediaItem in itemsToMigrate)
                {
                    try
                    {
                        if (!File.Exists(mediaItem.FilePath))
                        {
                            _logger.LogWarning("Source file not found: {FilePath}", mediaItem.FilePath);
                            failureCount++;
                            continue;
                        }

                        // Calculate new path preserving filename
                        var fileName = Path.GetFileName(mediaItem.FilePath);
                        var newFilePath = Path.Combine(newMediaRoot, fileName);

                        // Handle collision by appending guid if needed
                        if (File.Exists(newFilePath) && !FilesAreIdentical(mediaItem.FilePath, newFilePath))
                        {
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            var ext = Path.GetExtension(fileName);
                            newFilePath = Path.Combine(newMediaRoot, $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}");
                        }

                        // Move the file
                        File.Move(mediaItem.FilePath, newFilePath, overwrite: true);
                        _logger.LogInformation("Migrated {OldPath} to {NewPath}", mediaItem.FilePath, newFilePath);

                        // Update database record
                        mediaItem.FilePath = newFilePath;
                        await _mediaItemRepository.UpdateAsync(mediaItem, cancellationToken);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to migrate media item {Id} from {FilePath}", mediaItem.Id, mediaItem.FilePath);
                        failureCount++;
                    }
                }

                _logger.LogInformation("Migration complete: {SuccessCount} succeeded, {FailureCount} failed", successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during path migration");
                throw;
            }
        }

        private static bool IsLegacyPath(string filePath)
        {
            var pathLower = filePath.ToLowerInvariant();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLowerInvariant();
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).ToLowerInvariant();

            // If it's already in ProgramData/VideoForensics, it's not legacy
            if (pathLower.StartsWith(Path.Combine(commonAppData, "videoforensics").ToLowerInvariant()))
            {
                return false;
            }

            // Check for user profile paths (Pictures, Documents, OneDrive, etc.)
            if (pathLower.StartsWith(userProfile))
            {
                return true;
            }

            // Check for OneDrive environment variable paths
            string? oneDrive = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDrive) && pathLower.StartsWith(oneDrive.ToLowerInvariant()))
            {
                return true;
            }

            return false;
        }

        private static bool FilesAreIdentical(string path1, string path2)
        {
            var info1 = new FileInfo(path1);
            var info2 = new FileInfo(path2);
            return info1.Length == info2.Length && info1.LastWriteTimeUtc == info2.LastWriteTimeUtc;
        }
    }
}
