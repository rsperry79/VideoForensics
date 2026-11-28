using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Live-view still endpoints: upload a grabbed JPEG as a stored still, and read a stored still back.
    /// Both require the Review role (set on the route). Access is role-based; there is no per-camera rule.
    /// </summary>
    public static class LiveStillEndpoints
    {
        /// <summary>Largest JPEG accepted; matches the still store's limit.</summary>
        internal const long MaxJpegBytes = 20 * 1024 * 1024;

        private const string JpegContentType = "image/jpeg";

        /// <summary>
        /// POST /media/live-stills/{deviceId}: the body is the frame's JPEG bytes. Responds 201 with the still's
        /// location. 400 for an invalid JPEG, 404 for an unknown camera, 409 for a conflicting frame at the same
        /// instant, 413 for an oversized body, 415 for a non-JPEG content type.
        /// </summary>
        public static async Task<IResult> UploadAsync(
            Guid deviceId,
            HttpRequest request,
            ILiveStillCaptureService capture,
            HttpContext context,
            CancellationToken ct)
        {
            string? operatorId = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return Results.Unauthorized();
            }

            string? mediaType = request.ContentType?.Split(';')[0].Trim();
            if (!string.Equals(mediaType, JpegContentType, StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            byte[]? jpeg = await ReadBoundedAsync(request.Body, MaxJpegBytes, ct);
            if (jpeg is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            LiveStillResult result = await capture.CaptureAsync(deviceId, jpeg, operatorId, ct);

            return result.Error switch
            {
                LiveStillError.None => Results.Created($"/api/v1/media/{result.MediaItemId}/still", new { id = result.MediaItemId }),
                LiveStillError.NotFound => Results.NotFound(),
                LiveStillError.InvalidInput => Results.BadRequest(result.Message),
                LiveStillError.Conflict => Results.Conflict(result.Message),
                _ => Results.Problem(detail: result.Message, statusCode: StatusCodes.Status500InternalServerError)
            };
        }

        /// <summary>GET /media/{id}/still: the stored still's JPEG bytes. 404 unless the item is a stored live still that is on disk.</summary>
        public static async Task<IResult> GetStillAsync(
            Guid id,
            IMediaItemWriteRepository media,
            IMediaStorageProvider storage,
            CancellationToken ct)
        {
            MediaItem? item = await media.GetAsync(id, ct);
            if (item is null || item.MediaKind != MediaKinds.Still || item.IsPurged)
            {
                return Results.NotFound();
            }

            if (!await storage.ExistsAsync(item.FilePath, ct))
            {
                return Results.NotFound();
            }

            Stream content = await storage.OpenReadStreamAsync(item.FilePath, ct);
            return Results.Stream(content, JpegContentType);
        }

        /// <summary>Reads the body up to <paramref name="limit"/> bytes. Returns null when the body is longer, so the caller can refuse it.</summary>
        private static async Task<byte[]?> ReadBoundedAsync(Stream body, long limit, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > limit)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
    }
}
