using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IBackupImportService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/BackupEndpoints.cs) instead of performing import logic locally.
    /// The MAUI client uses this as part of the client/server split (§4/M5).
    /// </summary>
    public class RemoteBackupImportService : IBackupImportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteBackupImportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<BackupImportResult> ImportBackupAsync(string backupZipPath, string mediaRootPath, CancellationToken ct)
        {
            if (!System.IO.File.Exists(backupZipPath))
            {
                throw new FileNotFoundException($"Backup zip file not found: {backupZipPath}");
            }

            // Create multipart/form-data request
            using var formContent = new MultipartFormDataContent();
            using Stream fileStream = System.IO.File.OpenRead(backupZipPath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            formContent.Add(fileContent, "backupFile", Path.GetFileName(backupZipPath));

            // Add the media root path as a form field
            formContent.Add(new StringContent(mediaRootPath), "mediaRootPath");

            HttpResponseMessage response = await _httpClient.PostAsync("/api/v1/backup/import", formContent, ct);
            _ = response.EnsureSuccessStatusCode();

            BackupImportResultDto? dto = await response.Content.ReadFromJsonAsync<BackupImportResultDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null import result")).ToDomain();
        }
    }

    /// <summary>Request to import a backup archive into the database.</summary>
    /// <param name="MediaRootPath">Root path where imported media files will be stored.</param>
    internal record ImportBackupRequest(string MediaRootPath);
}
