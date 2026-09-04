using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Minimal API surface (plan §4/M5) wrapping the existing repository contracts one-for-one, plus
    /// a media-streaming endpoint - the read path a MAUI client (or any future paired device) uses
    /// instead of touching the server's database or provider directly. Every download/import still
    /// happens server-side only (§1); nothing here lets a caller trigger one yet - that's a later,
    /// separately-scoped write-path addition.
    ///
    /// SECURITY NOTE, stated explicitly rather than silently glossed over: these endpoints are
    /// currently UNAUTHENTICATED. This is a deliberate, temporary state for M5 ("prove the client/
    /// server split works before layering security on top" - plan §4/M6) - QR pairing, passkey auth,
    /// and network-tier gating land in M6. Until then, this API must only ever be reachable on a
    /// trusted local network (the Local tier), never exposed to a wider network or the internet.
    /// </summary>
    public static class MediaApiEndpoints
    {
        public static void MapMediaApiEndpoints(this WebApplication app)
        {
            app.MapGet("/api/devices", async (IDeviceRepository devices, CancellationToken ct) =>
                Results.Ok(await devices.ListAsync(ct)));

            app.MapGet("/api/media-items", async (Guid? deviceId, IMediaItemRepository mediaItems, CancellationToken ct) =>
            {
                var items = deviceId.HasValue
                    ? await mediaItems.GetByDeviceIdAsync(deviceId.Value, ct)
                    : await mediaItems.ListAsync(ct);
                return Results.Ok(items);
            });

            app.MapGet("/api/integrity-records", async (string mediaItemIds, IIntegrityRecordRepository integrityRecords, CancellationToken ct) =>
            {
                var ids = mediaItemIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                var records = await integrityRecords.GetLatestByMediaItemIdsAsync(ids, ct);
                return Results.Ok(records);
            });

            app.MapGet("/api/media/{id:guid}/content", async (Guid id, IMediaItemRepository mediaItems, IMediaStorageProvider storage, ILogger<Program> logger, CancellationToken ct) =>
            {
                var item = await mediaItems.GetAsync(id, ct);
                if (item == null)
                {
                    return Results.NotFound();
                }

                if (!await storage.ExistsAsync(item.FilePath, ct))
                {
                    logger.LogWarning("Media item {MediaItemId} has no file on disk at {FilePath}", id, item.FilePath);
                    return Results.NotFound();
                }

                var stream = await storage.OpenReadStreamAsync(item.FilePath, ct);
                var contentType = item.MediaFormat switch
                {
                    "video/mp4" or "mp4" => "video/mp4",
                    "image/jpeg" or "jpg" or "jpeg" => "image/jpeg",
                    _ => "application/octet-stream"
                };

                return Results.Stream(stream, contentType, item.FileName, enableRangeProcessing: true);
            });
        }
    }
}
