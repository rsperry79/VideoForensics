using System.Net;
using System.Net.Http.Headers;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ILiveStillCaptureService"/> for the MAUI client: posts the JPEG to
    /// POST /api/v1/media/live-stills/{deviceId}. The server derives the operator from the bearer token, so
    /// <c>operatorId</c> is ignored here (kept for interface compatibility).
    /// </summary>
    public sealed class RemoteLiveStillCaptureService : ILiveStillCaptureService
    {
        private readonly HttpClient _httpClient;

        public RemoteLiveStillCaptureService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<LiveStillResult> CaptureAsync(Guid deviceId, byte[] jpeg, string operatorId, CancellationToken ct)
        {
            using var content = new ByteArrayContent(jpeg);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            using HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/media/live-stills/{deviceId:D}", content, ct);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                string? location = response.Headers.Location?.ToString();
                Guid? id = ParseStillId(location);
                return id is Guid stillId
                    ? LiveStillResult.Ok(stillId)
                    : LiveStillResult.Fail(LiveStillError.StorageFailed, "The server did not return the still's location.");
            }

            string message = await response.Content.ReadAsStringAsync(ct);
            LiveStillError error = response.StatusCode switch
            {
                HttpStatusCode.NotFound => LiveStillError.NotFound,
                HttpStatusCode.BadRequest => LiveStillError.InvalidInput,
                HttpStatusCode.Conflict => LiveStillError.Conflict,
                _ => LiveStillError.StorageFailed
            };
            return LiveStillResult.Fail(error, string.IsNullOrWhiteSpace(message) ? $"Server returned {(int)response.StatusCode}." : message);
        }

        /// <summary>The still id from a "/api/v1/media/{id}/still" location, or null.</summary>
        private static Guid? ParseStillId(string? location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            string[] parts = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && Guid.TryParse(parts[^2], out Guid id) ? id : null;
        }
    }
}
