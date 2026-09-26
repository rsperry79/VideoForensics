using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IMediaItemRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). Backed endpoints: <see cref="ListAsync"/>, <see cref="GetByDeviceIdAsync"/>,
    /// <see cref="GetByDeviceAndDateRangeAsync"/>, and <see cref="GetAsync"/>. Every write method throws
    /// <see cref="NotSupportedException"/> because MAUI has no write path to server-owned evidence data -
    /// new media only ever originates server-side, from a download the server itself executed (a deliberate
    /// architectural rule from the plan, not a missing feature). Every other read with no matching
    /// endpoint also throws.
    /// </summary>
    public class RemoteMediaItemRepository : IMediaItemRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string NotSupportedMessage =
            "Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.";

        private const string NoWritePathMessage =
            "MAUI has no write path to server-owned evidence data - new media items only ever originate server-side, from a download the server itself executed.";

        private readonly HttpClient _httpClient;

        public RemoteMediaItemRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/media-items", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items?deviceId={deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items/{mediaItemId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            _ = response.EnsureSuccessStatusCode();
            MediaItemDto? dto = await response.Content.ReadFromJsonAsync<MediaItemDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string fromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string toEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items?deviceId={deviceId}&from={fromEscaped}&to={toEscaped}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }

        /// <inheritdoc />
        public Task<MediaItem?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            throw new NotSupportedException("Remote API doesn't yet support GetByApiSourceHashAsync");
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }

        /// <inheritdoc />
        public Task AddAsync(MediaItem mediaItem, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }

        /// <inheritdoc />
        public Task UpdateAsync(MediaItem mediaItem, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }

        /// <inheritdoc />
        public Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }
    }
}
