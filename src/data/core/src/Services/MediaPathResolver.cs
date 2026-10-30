using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// <see cref="IMediaPathResolver"/> over the configured media root. The root is read on every call, so a change on
    /// the Storage Settings page applies at once without a restart.
    /// </summary>
    public sealed class MediaPathResolver : IMediaPathResolver
    {
        private readonly IStorageLocationProvider _storage;
        private readonly IForensicsConfigurationService _configuration;

        /// <summary>Creates the resolver.</summary>
        public MediaPathResolver(IStorageLocationProvider storage, IForensicsConfigurationService configuration)
        {
            _storage = storage;
            _configuration = configuration;
        }

        /// <inheritdoc />
        public async Task<string> ToAbsoluteAsync(string storedPath, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(storedPath) || Path.IsPathRooted(storedPath))
            {
                return storedPath;
            }

            string root = await GetRootAsync(ct);
            return Path.GetFullPath(Path.Combine(root, storedPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <inheritdoc />
        public async Task<string> ToStoredAsync(string absolutePath, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(absolutePath) || !Path.IsPathRooted(absolutePath))
            {
                return absolutePath;
            }

            string root = Path.GetFullPath(await GetRootAsync(ct));
            string relative = Path.GetRelativePath(root, Path.GetFullPath(absolutePath));

            // Why: outside the root there is no relative form that stays valid, so keep the full path.
            return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
                ? absolutePath
                : relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private async Task<string> GetRootAsync(CancellationToken ct)
        {
            IForensicsConfiguration config = await _configuration.LoadConfigurationAsync(string.Empty, ct);
            return _storage.GetEffectiveRoot(StorageCategory.Media, config.DownloadLocation);
        }
    }
}
