using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IMediaBitrateSeriesSource"/>: calls GET /api/v1/media/{id}/bitrate-series with the paired
    /// device's bearer token. A 404 means the server cannot read the clip, and is reported as null.
    /// </summary>
    public sealed class RemoteMediaBitrateSeriesSource : IMediaBitrateSeriesSource
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteMediaBitrateSeriesSource(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<BitrateSecondDto>?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/media/{mediaItemId:D}/bitrate-series", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<BitrateSecondDto>>(JsonOptions, ct);
        }
    }
}
