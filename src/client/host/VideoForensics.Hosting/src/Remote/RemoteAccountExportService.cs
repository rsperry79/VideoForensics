using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IAccountExportService"/> that queues and polls account exports on the server
    /// (see VideoForensics.WebApp/Api/AccountEndpoints.cs). Client hosts use this instead of the server-tier
    /// export service.
    /// </summary>
    public class RemoteAccountExportService : IAccountExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;

        public RemoteAccountExportService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<Guid> QueueExportAsync(Guid accountId, bool purgeAfterExport, CancellationToken ct, string? stepUpToken = null)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/accounts/provider-accounts/{accountId}/exports")
            {
                Content = JsonContent.Create(new QueueAccountExportRequest(purgeAfterExport, ConfirmPurge: purgeAfterExport), options: JsonOptions)
            };
            if (stepUpToken is not null)
            {
                httpRequest.Headers.Add("X-StepUp-Token", stepUpToken);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();
            ExportJobDto? dto = await response.Content.ReadFromJsonAsync<ExportJobDto>(JsonOptions, ct);
            return dto?.JobId ?? throw new InvalidOperationException("The server returned no export job.");
        }

        /// <inheritdoc />
        public async Task<ExportJobStatus?> GetStatusAsync(Guid jobId, CancellationToken ct)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/exports/{jobId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            ExportJobDto? dto = await response.Content.ReadFromJsonAsync<ExportJobDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<ExportJobStatus?> GetLatestStatusForAccountAsync(Guid accountId, CancellationToken ct)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/{accountId}/export-status", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            ExportJobDto? dto = await response.Content.ReadFromJsonAsync<ExportJobDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }
    }
}
