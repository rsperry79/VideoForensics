using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IIntegrityRecordRepository"/> that calls the server's
    /// Minimal API (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs) instead of a local database -
    /// the MAUI client's implementation of the "thin client talks to a server API" half of the
    /// plan's client/server split (§4/M5). <see cref="AddAsync"/> throws
    /// <see cref="NotSupportedException"/> - integrity records are append-only server-side data
    /// produced by the server's own verification runs, not something a thin client writes.
    /// </summary>
    public class RemoteIntegrityRecordRepository : IIntegrityRecordRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteIntegrityRecordRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IntegrityRecord>> GetLatestByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct)
        {
            string ids = string.Join(',', mediaItemIds);
            var response = await _httpClient.GetAsync($"/api/v1/integrity-records?mediaItemIds={ids}", ct);
            _ = response.EnsureSuccessStatusCode();
            var dtos = await response.Content.ReadFromJsonAsync<List<IntegrityRecordDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public Task AddAsync(IntegrityRecord record, CancellationToken ct)
        {
            throw new NotSupportedException("MAUI has no write path to server-owned evidence data - integrity records are append-only data produced by the server's own verification runs, not something a thin client writes.");
        }
    }
}
