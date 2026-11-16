using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IBackupImportService"/>. Uploads the backup zip in chunks of at most
    /// 1 MB to the server's chunked import routes (/api/v1/backup/imports), then asks the server to assemble and
    /// import it. The server does the import, so no import logic runs on the client.
    /// </summary>
    public class RemoteBackupImportService : IBackupImportService
    {
        /// <summary>Size of each uploaded chunk, in bytes. The server rejects anything larger.</summary>
        public const int ChunkBytes = 1024 * 1024;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteBackupImportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<BackupImportResult> ImportBackupAsync(string backupZipPath, string mediaRootPath, CancellationToken ct)
        {
            if (!File.Exists(backupZipPath))
            {
                throw new FileNotFoundException($"Backup zip file not found: {backupZipPath}");
            }

            using HttpResponseMessage start = await _httpClient.PostAsync("/api/v1/backup/imports", null, ct);
            _ = start.EnsureSuccessStatusCode();
            BackupUploadCreatedDto created = await start.Content.ReadFromJsonAsync<BackupUploadCreatedDto>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Server returned no backup upload id");

            int chunkCount = await UploadChunksAsync(created.UploadId, backupZipPath, ct);

            var complete = new CompleteBackupUploadRequest(chunkCount, mediaRootPath);
            using HttpResponseMessage done = await _httpClient.PostAsJsonAsync($"/api/v1/backup/imports/{created.UploadId}/complete", complete, ct);
            _ = done.EnsureSuccessStatusCode();

            BackupImportResultDto? dto = await done.Content.ReadFromJsonAsync<BackupImportResultDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null import result")).ToDomain();
        }

        /// <summary>Sends the file as consecutive chunks, numbered from 0, and returns how many were sent.</summary>
        private async Task<int> UploadChunksAsync(Guid uploadId, string backupZipPath, CancellationToken ct)
        {
            using FileStream file = File.OpenRead(backupZipPath);
            byte[] buffer = new byte[ChunkBytes];
            int index = 0;
            while (true)
            {
                int read = await FillAsync(file, buffer, ct);
                if (read == 0)
                {
                    return index;
                }

                using var content = new ByteArrayContent(buffer, 0, read);
                using HttpResponseMessage put = await _httpClient.PutAsync($"/api/v1/backup/imports/{uploadId}/chunks/{index}", content, ct);
                _ = put.EnsureSuccessStatusCode();
                index++;
            }
        }

        /// <summary>Reads until the buffer is full or the file ends, so every chunk but the last is exactly full.</summary>
        private static async Task<int> FillAsync(Stream source, byte[] buffer, CancellationToken ct)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = await source.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
                if (n == 0)
                {
                    break;
                }

                total += n;
            }

            return total;
        }
    }
}
