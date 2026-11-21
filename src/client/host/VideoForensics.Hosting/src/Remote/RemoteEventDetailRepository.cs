using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>HTTP-backed <see cref="IEventDetailRepository"/> that calls <c>/api/v1/events/details</c>.</summary>
    public class RemoteEventDetailRepository : IEventDetailRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEventDetailRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<EventDetailDto>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
        {
            if (eventIds.Count == 0)
            {
                return [];
            }

            string query = string.Join('&', eventIds.Select(id => $"eventIds={id:D}"));
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/events/details?" + query, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDetailDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDetailDto>>(JsonOptions, ct);
            return dtos ?? [];
        }
    }
}
