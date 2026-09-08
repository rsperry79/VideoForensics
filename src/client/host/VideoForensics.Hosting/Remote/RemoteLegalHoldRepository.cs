using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ILegalHoldRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/EventEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). All methods delegate to the server's legal hold endpoints, which
    /// enforce step-up authentication for both place and release operations.
    /// </summary>
    public class RemoteLegalHoldRepository : ILegalHoldRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteLegalHoldRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<LegalHold> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct)
        {
            var request = new PlaceLegalHoldRequestDto(mediaItemId, reason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/legal-holds/", request, ct);
            _ = response.EnsureSuccessStatusCode();
            LegalHoldDto? dto = await response.Content.ReadFromJsonAsync<LegalHoldDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null response")).ToDomain();
        }

        /// <inheritdoc />
        public async Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct)
        {
            var request = new ReleaseLegalHoldRequestDto(legalHoldId, releaseReason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/legal-holds/release", request, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LegalHold>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct)
        {
            string ids = string.Join(',', mediaItemIds);
            string url = $"/api/v1/legal-holds/?mediaItemIds={Uri.EscapeDataString(ids)}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<LegalHoldDto>? dtos = await response.Content.ReadFromJsonAsync<List<LegalHoldDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }
    }
}
