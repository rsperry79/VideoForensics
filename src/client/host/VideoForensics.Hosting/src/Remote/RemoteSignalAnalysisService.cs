using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ISignalAnalysisService"/>: the MAUI client asks the server for the signal figures instead
    /// of computing them on the device. Calls GET /api/v1/signal-analysis with the paired device's bearer token.
    /// </summary>
    public sealed class RemoteSignalAnalysisService : ISignalAnalysisService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteSignalAnalysisService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<SignalAnalysis> AnalyzeAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(deviceIds);

            // Nothing to ask for: no cameras in scope means no figures, so skip the round trip.
            if (deviceIds.Count == 0)
            {
                return new SignalAnalysis([]);
            }

            string ids = Uri.EscapeDataString(string.Join(",", deviceIds.Select(id => id.ToString("D"))));
            string fromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string toEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/signal-analysis?devices={ids}&from={fromEscaped}&to={toEscaped}", ct);
            _ = response.EnsureSuccessStatusCode();

            SignalAnalysisDto? dto = await response.Content.ReadFromJsonAsync<SignalAnalysisDto>(JsonOptions, ct);
            return (dto ?? new SignalAnalysisDto([])).ToDomain();
        }
    }
}
