using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IMediaStillCaptureService"/> that calls the server's
    /// POST /api/v1/media/{id}/stills endpoint instead of extracting frames or touching a local
    /// database directly - the client's (MAUI) implementation of the "grab still with hash" write
    /// path. The capturedBy parameter is ignored client-side (kept for interface compatibility);
    /// the server derives the actor from the paired-device bearer token, matching every other
    /// Remote* write.
    /// </summary>
    public class RemoteMediaStillCaptureService : IMediaStillCaptureService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteMediaStillCaptureService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<MediaStillCaptureResult> CaptureStillAsync(
            Guid sourceMediaItemId,
            long frameOffsetMs,
            string? clientPngBase64,
            Guid? caseId,
            string? pinReason,
            string capturedBy,
            CancellationToken ct)
        {
            var request = new CaptureStillRequestDto(frameOffsetMs, clientPngBase64, caseId, pinReason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/media/{sourceMediaItemId}/stills", request, cancellationToken: ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.MediaNotFound, $"Media item {sourceMediaItemId} not found.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.InvalidInput, message);
            }

            if (!response.IsSuccessStatusCode)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                return MediaStillCaptureResult.Fail(
                    MediaStillCaptureError.ExtractionFailed,
                    string.IsNullOrWhiteSpace(message) ? $"Server returned {(int)response.StatusCode}." : message);
            }

            MediaStillDto? dto = await response.Content.ReadFromJsonAsync<MediaStillDto>(JsonOptions, ct);
            if (dto is null)
            {
                return MediaStillCaptureResult.Fail(MediaStillCaptureError.ExtractionFailed, "Server returned no content.");
            }

            return MediaStillCaptureResult.Ok(dto.ToDomain());
        }
    }
}
