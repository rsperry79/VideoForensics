using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IMediaFlagRepository"/> that calls the server's <c>/api/v1/media-flags</c> routes
    /// (see VideoForensics.WebApp/Api/MediaFlagEndpoints.cs), so a MAUI client reviews flags without a local database.
    /// Reviews send only the expected and new status and the reason: the server takes the operator from the bearer token,
    /// so the <c>operatorName</c> parameter is not sent. Flags are raised and reconciled only on the server, so those writes
    /// are not part of this class.
    /// </summary>
    public sealed class RemoteMediaFlagRepository : IMediaFlagRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteMediaFlagRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<MediaFlagDto?> GetAsync(Guid flagId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media-flags/{flagId:D}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MediaFlagDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<MediaFlagPageDto> ListPageAsync(MediaFlagFilterDto filter, int skip, int take, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(filter);
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/v1/media-flags?" + Query(filter, $"skip={skip}", $"take={take}"), ct);
            _ = response.EnsureSuccessStatusCode();
            MediaFlagPageDto? dto = await response.Content.ReadFromJsonAsync<MediaFlagPageDto>(JsonOptions, ct);
            return dto ?? new MediaFlagPageDto([], 0);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(MediaFlagFilterDto filter, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(filter);
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/media-flags/count?" + Query(filter), ct);
            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>(JsonOptions, ct);
        }

        /// <inheritdoc />
        /// <remarks>The server answers 204 when the flag is at the end of the list, and 404 when the flag itself is gone; both mean "no neighbour".</remarks>
        public async Task<MediaFlagDto?> GetNeighbourAsync(Guid flagId, MediaFlagFilterDto filter, MediaFlagNeighbourDto direction, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(filter);
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/v1/media-flags/{flagId:D}/neighbour?" + Query(filter, $"direction={direction}"), ct);
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MediaFlagDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        /// <remarks>The server answers 404 when the flag is missing or not in the filtered list; both mean "no position".</remarks>
        public async Task<int?> GetPositionAsync(Guid flagId, MediaFlagFilterDto filter, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(filter);
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/v1/media-flags/{flagId:D}/position?" + Query(filter), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>(JsonOptions, ct);
        }

        /// <inheritdoc />
        /// <remarks><paramref name="operatorName"/> is not sent; the server reads the operator from the token.</remarks>
        public async Task<MediaFlagDto> UpdateStatusAsync(Guid flagId, string expectedStatus, string newStatus, string? reason, string operatorName, CancellationToken ct)
        {
            var body = new SetFlagStatusRequestDto(expectedStatus, newStatus, reason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/media-flags/{flagId:D}/status", body, ct);
            await ThrowForFailureAsync(response, flagId, expectedStatus, ct);
            _ = response.EnsureSuccessStatusCode();
            return await ReadFlagAsync(response, ct);
        }

        /// <inheritdoc />
        /// <remarks><paramref name="operatorName"/> is not sent; the server reads the operator from the token.</remarks>
        public async Task<MediaFlagDto> ReopenAsync(Guid flagId, string operatorName, string reason, CancellationToken ct)
        {
            var body = new ReopenFlagRequestDto(reason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/media-flags/{flagId:D}/reopen", body, ct);
            await ThrowForFailureAsync(response, flagId, "Dismissed", ct);
            _ = response.EnsureSuccessStatusCode();
            return await ReadFlagAsync(response, ct);
        }

        /// <summary>Builds the shared filter query, then any extra parameters, with values escaped.</summary>
        private static string Query(MediaFlagFilterDto filter, params string[] extra)
        {
            var parts = new List<string>();
            if (filter.Types is { Count: > 0 })
            {
                parts.Add("types=" + string.Join(',', filter.Types));
            }

            if (filter.Statuses is { Count: > 0 })
            {
                parts.Add("statuses=" + string.Join(',', filter.Statuses));
            }

            if (filter.DeviceIds is { Count: > 0 })
            {
                parts.Add("deviceIds=" + string.Join(',', filter.DeviceIds.Select(id => id.ToString("D"))));
            }

            if (filter.FromUtc.HasValue)
            {
                parts.Add("fromUtc=" + Uri.EscapeDataString(filter.FromUtc.Value.ToString("O")));
            }

            if (filter.ToUtc.HasValue)
            {
                parts.Add("toUtc=" + Uri.EscapeDataString(filter.ToUtc.Value.ToString("O")));
            }

            parts.AddRange(extra);
            return string.Join('&', parts);
        }

        /// <summary>
        /// Maps the server's failure statuses back to the domain exceptions the repository contract documents: 409 becomes
        /// <see cref="FlagStatusConflictException"/> (with the server's reviewer and status), 404 <see cref="KeyNotFoundException"/>,
        /// 400 <see cref="ArgumentException"/>. Other failures are left for <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
        /// </summary>
        private static async Task ThrowForFailureAsync(HttpResponseMessage response, Guid flagId, string expectedStatus, CancellationToken ct)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Conflict:
                    FlagStatusConflictDto? body = await response.Content.ReadFromJsonAsync<FlagStatusConflictDto>(JsonOptions, ct);
                    throw (body ?? new FlagStatusConflictDto(expectedStatus, expectedStatus, null, null)).ToDomain(flagId);
                case HttpStatusCode.NotFound:
                    throw new KeyNotFoundException("Media flag not found.");
                case HttpStatusCode.BadRequest:
                    throw new ArgumentException("The server rejected the media flag change.");
            }
        }

        private static async Task<MediaFlagDto> ReadFlagAsync(HttpResponseMessage response, CancellationToken ct)
        {
            MediaFlagDto? dto = await response.Content.ReadFromJsonAsync<MediaFlagDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("The server returned no media flag.");
        }
    }
}
