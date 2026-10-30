using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IEventDetailRepository"/> that calls <c>/api/v1/events/details</c> on the server, so the
    /// MAUI client's Detail tab reads the same detection rows the server holds.
    /// </summary>
    public class RemoteEventDetailRepository : IEventDetailRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEventDetailRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        /// <remarks>Empty input returns empty without a request. The server enforces the 200-id limit.</remarks>
        public async Task<IReadOnlyList<EventDetail>> GetDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
        {
            if (eventIds.Count == 0)
            {
                return [];
            }

            string query = string.Join('&', eventIds.Select(id => $"eventIds={id:D}"));
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/events/details?" + query, ct);
            _ = response.EnsureSuccessStatusCode();
            List<EventDetailDto>? dtos = await response.Content.ReadFromJsonAsync<List<EventDetailDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }
    }
}
