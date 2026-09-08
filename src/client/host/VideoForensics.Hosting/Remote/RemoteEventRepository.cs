using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IEventRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/EventEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). Read methods that have server endpoints are implemented; write operations
    /// and reads without endpoints throw <see cref="NotSupportedException"/>.
    /// </summary>
    public class RemoteEventRepository : IEventRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string NotSupportedMessage =
            "Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.";

        private const string NoWritePathMessage =
            "MAUI has no write path to server-owned evidence data - new events only ever originate server-side.";

        private readonly HttpClient _httpClient;

        public RemoteEventRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<Event?> GetAsync(Guid eventId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/events/{eventId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            _ = response.EnsureSuccessStatusCode();
            EventDto? dto = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }

        /// <inheritdoc />
        public async Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            var dto = new EventDto(
                @event.Id,
                @event.DeviceId,
                @event.ProviderEventId,
                @event.EventType,
                @event.OccurredAtUtc,
                @event.SnapshotUrl,
                @event.MetadataJson,
                @event.DiscoveredAtUtc,
                @event.DownloadedAtUtc,
                @event.ApiSourceHash,
                @event.EventIntegrityHash
            );
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/events", dto, ct);
            _ = response.EnsureSuccessStatusCode();
            EventDto? resultDto = await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions, ct);
            return (resultDto ?? throw new InvalidOperationException("Server returned null response")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string url = $"/api/v1/events/by-device/{deviceId}?fromUtc={fromUtc:O}&toUtc={toUtc:O}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string url = $"/api/v1/events/by-location/{locationId}?fromUtc={fromUtc:O}&toUtc={toUtc:O}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string url = $"/api/v1/events/by-type/{Uri.EscapeDataString(eventType)}?deviceId={deviceId}&fromUtc={fromUtc:O}&toUtc={toUtc:O}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string url = $"/api/v1/events/by-type/{Uri.EscapeDataString(eventType)}?locationId={locationId}&fromUtc={fromUtc:O}&toUtc={toUtc:O}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string url = $"/api/v1/events/summary/{locationId}?fromUtc={fromUtc:O}&toUtc={toUtc:O}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            Dictionary<string, int>? summary = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, ct);
            return summary ?? new Dictionary<string, int>();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/events/unanswered/{deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/events/", ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/v1/events/{eventId}", ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public Task<Event> CreateAsync(Event @event, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }

        /// <inheritdoc />
        public Task UpdateAsync(Event @event, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }
    }
}
