namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Converts between the path stored on a media row and the full path on disk. Rows hold paths relative to the
    /// media root, so moving the root (or restoring onto another machine) does not strand them.
    /// </summary>
    public interface IMediaPathResolver
    {
        /// <summary>
        /// Full path for a stored path. A path that is already rooted (a row written before relative paths) is returned
        /// unchanged.
        /// </summary>
        Task<string> ToAbsoluteAsync(string storedPath, CancellationToken ct);

        /// <summary>
        /// Path to store for a full path: relative to the media root when it is under it, otherwise unchanged. Separators
        /// are normalized to '/' so the stored value is the same on every platform.
        /// </summary>
        Task<string> ToStoredAsync(string absolutePath, CancellationToken ct);
    }

    /// <summary>Resolver that leaves every path as given. The default where no media root is known, such as in tests.</summary>
    public sealed class PassThroughMediaPathResolver : IMediaPathResolver
    {
        /// <summary>Shared instance.</summary>
        public static readonly PassThroughMediaPathResolver Instance = new();

        /// <inheritdoc />
        public Task<string> ToAbsoluteAsync(string storedPath, CancellationToken ct)
        {
            return Task.FromResult(storedPath);
        }

        /// <inheritdoc />
        public Task<string> ToStoredAsync(string absolutePath, CancellationToken ct)
        {
            return Task.FromResult(absolutePath);
        }
    }
}
