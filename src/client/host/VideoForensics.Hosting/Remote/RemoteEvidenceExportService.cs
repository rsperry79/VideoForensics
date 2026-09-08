using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IEvidenceExportService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/EvidenceEndpoints.cs) instead of performing export logic locally.
    /// The MAUI client uses this as part of the client/server split (§4/M5).
    /// </summary>
    public class RemoteEvidenceExportService : IEvidenceExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEvidenceExportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<ExportResult> ExportEvidenceAsync(
            IReadOnlyList<Guid> mediaItemIds,
            string outputDirectory,
            string? caseReference,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct)
        {
            var request = new ExportEvidenceRequest(mediaItemIds, outputDirectory, caseReference, recipientDescription, passphrase);
            var jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/evidence/export")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            ExportResultDto? dto = await response.Content.ReadFromJsonAsync<ExportResultDto>(JsonOptions, ct);
            ExportResultDto result = dto ?? throw new InvalidOperationException("Server returned null export result");
            return new ExportResult
            {
                Success = result.Success,
                ArchivePath = result.ArchivePath,
                ArchiveSha256Hash = result.ArchiveSha256Hash,
                ItemsIncluded = result.ItemsIncluded,
                ItemsExcludedForFailedIntegrity = result.ItemsExcludedForFailedIntegrity,
                ErrorMessage = result.ErrorMessage
            };
        }
    }

    /// <summary>Request to export media items to an archive.</summary>
    /// <param name="MediaItemIds">IDs of media items to export.</param>
    /// <param name="OutputDirectory">Output directory for the archive.</param>
    /// <param name="CaseReference">Case reference number for the export metadata.</param>
    /// <param name="RecipientDescription">Description of the archive recipient/purpose.</param>
    /// <param name="Passphrase">Optional passphrase for AES-256 encryption.</param>
    internal record ExportEvidenceRequest(
        IReadOnlyList<Guid> MediaItemIds,
        string OutputDirectory,
        string? CaseReference,
        string? RecipientDescription,
        string? Passphrase
    );
}
