using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IBackupExportService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/BackupEndpoints.cs) instead of performing export logic locally.
    /// The MAUI client uses this as part of the client/server split (§4/M5).
    /// </summary>
    public class RemoteBackupExportService : IBackupExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteBackupExportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<PrepareExportResult> PrepareForExportAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/v1/backup/prepare-export", null, ct);
            _ = response.EnsureSuccessStatusCode();

            PrepareExportResultDto? dto = await response.Content.ReadFromJsonAsync<PrepareExportResultDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null prepare-export result")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<BackupExportResult> ExportBackupAsync(string outputDirectory, CancellationToken ct)
        {
            var request = new ExportBackupRequest(outputDirectory);
            string jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/backup/export")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            // The endpoint may return either a streamed zip file or JSON with the result
            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (contentType.Contains("application/zip", StringComparison.OrdinalIgnoreCase))
            {
                // Response is a zip stream - read it and save to outputDirectory
                string? fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
                }

                string outputPath = Path.Combine(outputDirectory, fileName);
                _ = Directory.CreateDirectory(outputDirectory);

                using (Stream contentStream = await response.Content.ReadAsStreamAsync(ct))
                using (FileStream fileStream = System.IO.File.Create(outputPath))
                {
                    await contentStream.CopyToAsync(fileStream, ct);
                }

                // Return a result indicating success with the local path
                return new BackupExportResult
                {
                    Success = true,
                    ArchivePath = outputPath,
                    ArchiveSha256Hash = null, // Client-side: would need to hash the file if required
                    ProviderAccountCount = 0,
                    LocationCount = 0,
                    DeviceCount = 0,
                    EventCount = 0,
                    DownloadEventCount = 0,
                    MediaItemCount = 0,
                    ErrorMessage = null
                };
            }
            else
            {
                // Response is JSON with BackupExportResultDto
                BackupExportResultDto? dto = await response.Content.ReadFromJsonAsync<BackupExportResultDto>(JsonOptions, ct);
                return (dto ?? throw new InvalidOperationException("Server returned null export result")).ToDomain();
            }
        }
    }

    /// <summary>Request to export the database to a backup archive.</summary>
    /// <param name="OutputDirectory">Output directory where the backup archive will be created.</param>
    internal record ExportBackupRequest(string OutputDirectory);
}
