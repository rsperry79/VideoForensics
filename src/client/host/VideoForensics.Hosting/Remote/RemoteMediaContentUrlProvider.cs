using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IMediaContentUrlProvider"/> that requests media access tickets
    /// from the server and returns browser-loadable URLs with embedded tickets. Used by MAUI client to generate
    /// src attributes for img and video tags that can load media without carrying Authorization headers.
    /// </summary>
    public class RemoteMediaContentUrlProvider : IMediaContentUrlProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteMediaContentUrlProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyDictionary<Guid, string>> GetContentUrlsAsync(IReadOnlyCollection<Guid> mediaItemIds, CancellationToken ct)
        {
            if (mediaItemIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            // Deduplicate IDs
            var distinctIds = mediaItemIds.Distinct().ToList();
            var result = new Dictionary<Guid, string>();

            // Chunk into batches of MaxTicketsPerRequest
            for (int i = 0; i < distinctIds.Count; i += MediaContentRoutes.MaxTicketsPerRequest)
            {
                var batch = distinctIds.Skip(i).Take(MediaContentRoutes.MaxTicketsPerRequest).ToList();
                var request = new MediaTicketRequestDto(batch);

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/v1/media/tickets",
                    request,
                    JsonOptions,
                    ct
                );

                _ = response.EnsureSuccessStatusCode();

                List<MediaTicketDto>? tickets = await response.Content.ReadFromJsonAsync<List<MediaTicketDto>>(JsonOptions, ct);
                if (tickets != null)
                {
                    foreach (var ticket in tickets)
                    {
                        string contentUrl = ticket.ContentUrl;

                        // Convert to absolute URL if BaseAddress is set; otherwise return relative URL
                        if (_httpClient.BaseAddress != null)
                        {
                            contentUrl = new Uri(_httpClient.BaseAddress, contentUrl).ToString();
                        }

                        result[ticket.MediaItemId] = contentUrl;
                    }
                }
            }

            return result;
        }
    }
}
