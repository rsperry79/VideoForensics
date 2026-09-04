using System.Net.Http.Json;
using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IMediaItemRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). Only <see cref="ListAsync"/> and <see cref="GetByDeviceIdAsync"/> are
    /// backed by real endpoints. Every write method throws <see cref="NotSupportedException"/>
    /// because MAUI has no write path to server-owned evidence data - new media only ever
    /// originates server-side, from a download the server itself executed (a deliberate
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
            var response = await _httpClient.GetAsync("/api/media-items", ct);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<MediaItem>>(JsonOptions, ct);
            return items ?? [];
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            var response = await _httpClient.GetAsync($"/api/media-items?deviceId={deviceId}", ct);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<MediaItem>>(JsonOptions, ct);
            return items ?? [];
        }

        /// <inheritdoc />
        public Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct) =>
            throw new NotSupportedException(NotSupportedMessage);

        /// <inheritdoc />
        public Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            throw new NotSupportedException(NotSupportedMessage);

        /// <inheritdoc />
        public Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct) =>
            throw new NotSupportedException(NotSupportedMessage);

        /// <inheritdoc />
        public Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct) =>
            throw new NotSupportedException(NotSupportedMessage);

        /// <inheritdoc />
        public Task AddAsync(MediaItem mediaItem, CancellationToken ct) =>
            throw new NotSupportedException(NoWritePathMessage);

        /// <inheritdoc />
        public Task UpdateAsync(MediaItem mediaItem, CancellationToken ct) =>
            throw new NotSupportedException(NoWritePathMessage);

        /// <inheritdoc />
        public Task DeleteAsync(Guid mediaItemId, CancellationToken ct) =>
            throw new NotSupportedException(NoWritePathMessage);
    }
}
