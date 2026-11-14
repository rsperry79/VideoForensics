using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IMediaItemRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs). Backed reads: <see cref="ListAsync"/>,
    /// <see cref="GetByDeviceIdAsync"/>, <see cref="GetByDeviceAndDateRangeAsync"/>, <see cref="GetAsync"/> and
    /// <see cref="GetPageByDevicesAndDateRangeAsync"/>. Every other read needs a server-side query and throws
    /// <see cref="NotSupportedException"/>. Writes are server-only and are not part of this interface.
    /// </summary>
    public class RemoteMediaItemRepository : IMediaItemRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string ServerOnlyMessage = "Not supported on a remote (MAUI client) repository - this read runs on the server only.";

        private readonly HttpClient _httpClient;

        public RemoteMediaItemRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/media-items", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<MediaItemPageDto> GetPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false)
        {
            var parameters = deviceIds.Select(id => $"deviceIds={id:D}").ToList();
            parameters.Add($"from={Uri.EscapeDataString(fromUtc.ToString("o"))}");
            parameters.Add($"to={Uri.EscapeDataString(toUtc.ToString("o"))}");
            parameters.Add($"skip={skip}");
            parameters.Add($"take={take}");
            if (videoOnly)
            {
                parameters.Add("videoOnly=true");
            }

            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/media-items/page?" + string.Join('&', parameters), ct);
            _ = response.EnsureSuccessStatusCode();
            MediaItemPageResponse? body = await response.Content.ReadFromJsonAsync<MediaItemPageResponse>(JsonOptions, ct);
            return new MediaItemPageDto((body?.Items ?? []).ToList(), body?.TotalCount ?? 0);
        }

        /// <summary>Wire shape of <c>/media-items/page</c>.</summary>
        private sealed record MediaItemPageResponse(List<MediaItemDto>? Items, int TotalCount);

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items?deviceId={deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<MediaItemDto?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items/{mediaItemId}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MediaItemDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaItemDto>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string fromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string toEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-items?deviceId={deviceId}&from={fromEscaped}&to={toEscaped}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<MediaItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaItemDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        /// <remarks>No server endpoint returns only snapshots yet, so this filters the device's range on the client.</remarks>
        public async Task<IReadOnlyList<MediaItemDto>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            IReadOnlyList<MediaItemDto> range = await GetByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, ct);
            return range
                .Where(m => string.Equals(m.MediaFormat, "jpg", StringComparison.OrdinalIgnoreCase)
                    && m.MetadataJson is not null && m.MetadataJson.Contains("\"type\":\"snapshot\"", StringComparison.Ordinal))
                .OrderBy(m => m.RecordedAtUtc)
                .ToList();
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<MediaItemDto>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            throw new NotSupportedException(ServerOnlyMessage);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> GetDistinctVideoResolutionsAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <inheritdoc />
        public Task<MediaItemDto?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct)
        {
            throw new NotSupportedException(ServerOnlyMessage);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<MeasuredBitrateDto>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            throw new NotSupportedException(ServerOnlyMessage);
        }

        /// <inheritdoc />
        public Task<AllTimeBitrateBaselineDto> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            throw new NotSupportedException(ServerOnlyMessage);
        }
    }
}
