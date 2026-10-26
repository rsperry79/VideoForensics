using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Outcome of one <see cref="MediaStorageMigrator.MigrateAsync"/> run.</summary>
    /// <param name="FilesMoved">Files moved from the legacy root into the media root.</param>
    /// <param name="FilesSkipped">Files left in place: a destination already existed, the file was missing, or its device could not be resolved.</param>
    /// <param name="LegacyRootRemoved">True when the legacy root was empty afterwards and was deleted.</param>
    public sealed record MediaMigrationResult(int FilesMoved, int FilesSkipped, bool LegacyRootRemoved);

    /// <summary>
    /// Moves media from the legacy <c>DownloadLocation</c> into the service media root, laid out as
    /// <c>&lt;provider&gt;\&lt;location&gt;\&lt;device&gt;</c>. Runs on every startup: once the legacy folder is gone it is a no-op.
    /// Only empty folders are removed, and an existing destination file is never overwritten.
    /// </summary>
    public sealed class MediaStorageMigrator
    {
        private readonly IStorageLocationProvider _storage;
        private readonly IForensicsConfigurationService _configurationService;
        private readonly IMediaItemRepository _mediaItems;
        private readonly IDeviceRepository _devices;
        private readonly ILocationRepository _locations;
        private readonly ILogger<MediaStorageMigrator> _logger;

        public MediaStorageMigrator(
            IStorageLocationProvider storage,
            IForensicsConfigurationService configurationService,
            IMediaItemRepository mediaItems,
            IDeviceRepository devices,
            ILocationRepository locations,
            ILogger<MediaStorageMigrator> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _mediaItems = mediaItems ?? throw new ArgumentNullException(nameof(mediaItems));
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Moves legacy media into the media root and rewrites its database paths.</summary>
        public async Task<MediaMigrationResult> MigrateAsync(CancellationToken cancellationToken)
        {
            IForensicsConfiguration config = await _configurationService.LoadConfigurationAsync("", cancellationToken);
            if (string.IsNullOrWhiteSpace(config.DownloadLocation))
            {
                return new MediaMigrationResult(0, 0, false);
            }

            string legacyRoot = TrimTrailingSeparator(Path.GetFullPath(config.DownloadLocation));
            string targetRoot = TrimTrailingSeparator(Path.GetFullPath(_storage.GetDefaultRoot(StorageCategory.Media)));
            if (string.Equals(legacyRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new MediaMigrationResult(0, 0, false);
            }

            if (!Directory.Exists(legacyRoot))
            {
                _logger.LogDebug("Legacy media folder {LegacyRoot} does not exist; nothing to migrate", legacyRoot);
                return new MediaMigrationResult(0, 0, false);
            }

            int moved = 0;
            int skipped = 0;
            foreach (Data.Common.Entities.MediaItem item in await _mediaItems.ListAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsUnder(item.FilePath, legacyRoot))
                {
                    continue;
                }

                string? deviceFolder = await ResolveDeviceFolderAsync(item.DeviceId, targetRoot, cancellationToken);
                if (deviceFolder is null)
                {
                    _logger.LogWarning("Media item {MediaItemId} has no resolvable device; left in legacy folder", item.Id);
                    skipped++;
                    continue;
                }

                string destination = Path.Combine(deviceFolder, Path.GetFileName(item.FilePath));
                if (!File.Exists(item.FilePath) || File.Exists(destination))
                {
                    // Keep the row pointing at the legacy file: moving over an existing file would lose it.
                    skipped++;
                    continue;
                }

                _ = Directory.CreateDirectory(deviceFolder);
                File.Move(item.FilePath, destination);
                item.FilePath = destination;
                await _mediaItems.UpdateAsync(item, cancellationToken);
                moved++;
            }

            bool legacyRemoved = RemoveEmptyFolders(legacyRoot);
            if (legacyRemoved)
            {
                // The override pointed at the legacy root; clearing it makes the media root the single source of truth.
                config.DownloadLocation = null;
                await _configurationService.SaveConfigurationAsync(config, cancellationToken);
            }

            _logger.LogInformation(
                "Media migration: moved {Moved} file(s), skipped {Skipped}, legacy folder removed: {Removed}",
                moved, skipped, legacyRemoved);
            return new MediaMigrationResult(moved, skipped, legacyRemoved);
        }

        private async Task<string?> ResolveDeviceFolderAsync(Guid deviceId, string targetRoot, CancellationToken cancellationToken)
        {
            Data.Common.Entities.Device? device = await _devices.GetAsync(deviceId, cancellationToken);
            if (device is null)
            {
                return null;
            }

            Data.Common.Entities.Location? location = await _locations.GetAsync(device.LocationId, cancellationToken);
            if (location is null)
            {
                return null;
            }

            IReadOnlyList<Data.Common.Entities.ProviderAccount> accounts =
                await _locations.ListProviderAccountsForLocationAsync(location.Id, cancellationToken);
            string providerName = accounts.FirstOrDefault()?.ProviderName ?? "unknown";
            return MediaFolderLayout.GetDeviceFolder(targetRoot, providerName, location.Name, device.Name);
        }

        /// <summary>Deletes empty folders under <paramref name="root"/>, deepest first, then the root if it is empty too.</summary>
        private static bool RemoveEmptyFolders(string root)
        {
            foreach (string folder in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(f => f.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder, recursive: false);
                }
            }

            if (Directory.EnumerateFileSystemEntries(root).Any())
            {
                return false;
            }

            Directory.Delete(root, recursive: false);
            return true;
        }

        private static bool IsUnder(string path, string root)
        {
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
