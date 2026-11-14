using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IReportGenerationService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/ReportEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). All report generation methods call corresponding `/api/v1/reports/...`
    /// GET endpoints; WriteReportAsync calls POST `/api/v1/reports/write`.
    /// </summary>
    public class RemoteReportGenerationService : IReportGenerationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteReportGenerationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<EvidenceReviewReportDto> BuildEvidenceReviewAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            string queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/evidence-review{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            EvidenceReviewReportDto? dto = await response.Content.ReadFromJsonAsync<EvidenceReviewReportDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Response was null");
        }

        /// <inheritdoc />
        public async Task<ForensicAnalysisReportDto> BuildForensicAnalysisReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            string queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/forensic-analysis{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            ForensicAnalysisReportDto? dto = await response.Content.ReadFromJsonAsync<ForensicAnalysisReportDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Response was null");
        }

        /// <inheritdoc />
        public async Task<SignalAnomalyReportDto> BuildSignalAnomalyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            string queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/signal-anomaly{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            SignalAnomalyReportDto? dto = await response.Content.ReadFromJsonAsync<SignalAnomalyReportDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Response was null");
        }

        /// <inheritdoc />
        public async Task<AccessControlReportDto> BuildAccessControlReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            string queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/access-control{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            AccessControlReportDto? dto = await response.Content.ReadFromJsonAsync<AccessControlReportDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Response was null");
        }

        /// <inheritdoc />
        public async Task<ChainOfCustodyReportDto> BuildChainOfCustodyReportAsync(
            Guid? deviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            string queryString = BuildQueryString(deviceId, fromUtc, toUtc);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/reports/chain-of-custody{queryString}", ct);
            _ = response.EnsureSuccessStatusCode();
            ChainOfCustodyReportDto? dto = await response.Content.ReadFromJsonAsync<ChainOfCustodyReportDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Response was null");
        }

        /// <inheritdoc />
        public async Task WriteReportAsync(
            object reportDto,
            string format,
            CancellationToken ct)
        {
            // Anonymous type matching WriteReportRequest from ReportEndpoints
            var request = new { ReportDto = reportDto, Format = format };
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/reports/write", request, JsonOptions, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        private static string BuildQueryString(Guid? deviceId, DateTime fromUtc, DateTime toUtc)
        {
            var parameters = new List<string>();
            if (deviceId.HasValue)
            {
                parameters.Add($"deviceId={deviceId.Value:D}");
            }

            parameters.Add($"fromUtc={fromUtc:O}");
            parameters.Add($"toUtc={toUtc:O}");
            return "?" + string.Join("&", parameters);
        }
    }
}
