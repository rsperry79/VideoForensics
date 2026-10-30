using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Today's (M5) only IMediaStorageProvider implementation - plain local-disk File I/O, matching
    /// how the download pipeline already stores media today. Registered by every server-tier host.
    /// </summary>
    public class LocalDiskMediaStorageProvider : IMediaStorageProvider
    {
        private readonly IMediaPathResolver _paths;

        /// <summary>Creates the provider. Stored paths are relative to the media root, so each call resolves its path first.</summary>
        public LocalDiskMediaStorageProvider(IMediaPathResolver? paths = null)
        {
            _paths = paths ?? PassThroughMediaPathResolver.Instance;
        }

        public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken ct)
        {
            path = await _paths.ToAbsoluteAsync(path, ct);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            return stream;
        }

        public async Task<bool> ExistsAsync(string path, CancellationToken ct)
        {
            return File.Exists(await _paths.ToAbsoluteAsync(path, ct));
        }

        public async Task SaveAsync(string path, Stream content, CancellationToken ct)
        {
            path = await _paths.ToAbsoluteAsync(path, ct);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await content.CopyToAsync(fileStream, ct);
        }

        public async Task DeleteAsync(string path, CancellationToken ct)
        {
            path = await _paths.ToAbsoluteAsync(path, ct);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
