using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IJammingRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/JammingEndpoints.cs) instead of a local database - the client's
    /// implementation of the "thin client talks to a server API" half of the client/server split.
    ///
    /// Writes only ever send an <see cref="UpsertJammingIncidentRequest"/>: <c>CaseId</c> and
    /// <c>DetectedAtUtc</c> are server-controlled and never put on the wire, so the saved record the server
    /// returns (including the case it auto-created for a new incident) is authoritative. <c>Source</c> IS
    /// sent so auto-detected incidents keep their provenance (the server applies it to new incidents only).
    /// </summary>
    public class RemoteJammingRepository : IJammingRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteJammingRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<JammingIncidentDto> UpsertIncidentAsync(JammingIncidentDto incident, CancellationToken ct)
        {
            var request = new UpsertJammingIncidentRequest(
                incident.Id,
                incident.DeviceId,
                incident.StartUtc,
                incident.EndUtc,
                incident.AffectedEventCount,
                incident.AverageDegradationDb,
                incident.Confidence,
                incident.Notes,
                incident.Source);

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync("/api/v1/jamming/incidents", request, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
            JammingIncidentDto? dto = await response.Content.ReadFromJsonAsync<JammingIncidentDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingIncidentDto>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var url = new StringBuilder("/api/v1/jamming/incidents");
            char separator = '?';
            if (deviceId.HasValue)
            {
                _ = url.Append(separator).Append("deviceId=").Append(deviceId.Value);
                separator = '&';
            }

            if (fromUtc.HasValue)
            {
                _ = url.Append(separator).Append("fromUtc=").Append(Uri.EscapeDataString(fromUtc.Value.ToString("O")));
                separator = '&';
            }

            if (toUtc.HasValue)
            {
                _ = url.Append(separator).Append("toUtc=").Append(Uri.EscapeDataString(toUtc.Value.ToString("O")));
            }

            HttpResponseMessage response = await _httpClient.GetAsync(url.ToString(), ct);
            _ = response.EnsureSuccessStatusCode();
            List<JammingIncidentDto>? dtos = await response.Content.ReadFromJsonAsync<List<JammingIncidentDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<JammingIncidentDto?> GetIncidentAsync(Guid incidentId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/jamming/incidents/{incidentId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JammingIncidentDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<JammingStatsSummaryDto?> GetStatsAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/jamming/stats/{deviceId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JammingStatsSummaryDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingStatsSummaryDto>> ListStatsAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/jamming/stats", ct);
            _ = response.EnsureSuccessStatusCode();
            List<JammingStatsSummaryDto>? dtos = await response.Content.ReadFromJsonAsync<List<JammingStatsSummaryDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<JammingStatsSummaryDto> RecomputeStatsAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/jamming/stats/{deviceId}/recompute", null, ct);
            _ = response.EnsureSuccessStatusCode();
            JammingStatsSummaryDto? dto = await response.Content.ReadFromJsonAsync<JammingStatsSummaryDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }
    }
}
