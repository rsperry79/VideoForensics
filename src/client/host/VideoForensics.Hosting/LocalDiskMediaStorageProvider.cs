using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Today's (M5) only IMediaStorageProvider implementation - plain local-disk File I/O, matching
    /// how the download pipeline already stores media today. Registered by every server-tier host.
    /// </summary>
    public class LocalDiskMediaStorageProvider : IMediaStorageProvider
    {
        public Task<Stream> OpenReadStreamAsync(string path, CancellationToken ct)
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            return Task.FromResult(stream);
        }

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(File.Exists(path));

        public async Task SaveAsync(string path, Stream content, CancellationToken ct)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await content.CopyToAsync(fileStream, ct);
        }

        public Task DeleteAsync(string path, CancellationToken ct)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }
    }
}
