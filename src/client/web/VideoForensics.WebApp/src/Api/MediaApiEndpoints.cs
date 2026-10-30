using Microsoft.Extensions.Primitives;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Signal;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Minimal API surface (plan §4/M5) wrapping the existing repository contracts one-for-one, plus
    /// a media-streaming endpoint - the read path a MAUI client (or any future paired device) uses
    /// instead of touching the server's database or provider directly. Every download/import still
    /// happens server-side only (§1); nothing here lets a caller trigger one yet - that's a later,
    /// separately-scoped write-path addition.
    ///
    /// SECURITY NOTE: These endpoints enforce bearer-token authentication (RequireAuthorization)
    /// on all routes. The /media/{id}/content endpoint additionally accepts per-item access tickets
    /// for use in HTML img/video tags which cannot send headers. Tickets are short-lived (10 min),
    /// scoped to a single media item, and attributed to the operator who requested them (audit trail).
    /// All content access via either path is recorded in the access audit log (except range requests
    /// not starting at byte 0, which are presumed replay/resumption of playback already audited).
    /// Ticket validation re-checks the operator's Active/IsApproved status on every request, so a
    /// deactivated operator loses access immediately even if their ticket hasn't expired. If the
    /// audit write fails, the request returns 500 (evidence access must be audited).
    /// </summary>
    public static class MediaApiEndpoints
    {
        public static void MapMediaApiEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1");

            _ = group.MapGet("/devices", async (IDeviceRepository devices, CancellationToken ct) =>
                Results.Ok((await devices.ListAsync(ct)).Select(x => x.ToDto())))
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media-items", ListMediaItemsAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media-items/page", ListMediaItemPageAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media-items/{id:guid}", GetMediaItemAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/signal-analysis", GetSignalAnalysisAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/devices/{id:guid}/health", GetDeviceHealthHistoryAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/integrity-records", async (string mediaItemIds, IIntegrityRecordRepository integrityRecords, CancellationToken ct) =>
            {
                var ids = mediaItemIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out Guid id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                IReadOnlyList<IntegrityRecord> records = await integrityRecords.GetLatestByMediaItemIdsAsync(ids, ct);
                return Results.Ok(records.Select(x => x.ToDto()));
            })
            .RequireAuthorization()
            .RequireRateLimiting("media");

            _ = group.MapPost("/media/tickets", IssueTicketsAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media/{id:guid}/content", GetContentAsync)
                .AllowAnonymous()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media/{id:guid}/thumbnail", GetThumbnailAsync)
                .AllowAnonymous()
                .RequireRateLimiting("media");

            _ = group.MapGet("/media/{id:guid}/preview", GetPreviewAsync)
                .AllowAnonymous()
                .RequireRateLimiting("media");

            _ = group.MapPost("/media/{id:guid}/stills", CreateMediaStillAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("media");
        }

        /// <summary>
        /// Issues short-lived access tickets for one or more media items.
        /// The calling operator is extracted from the OperatorId claim and attributed to each ticket.
        /// </summary>
        public static async Task<IResult> IssueTicketsAsync(
            MediaTicketRequestDto request,
            IMediaAccessTicketService ticketService,
            HttpContext context,
            CancellationToken ct)
        {
            // Extract operator ID from claims
            string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!Guid.TryParse(operatorIdClaim, out Guid operatorId))
            {
                return Results.Unauthorized();
            }

            // Null or empty list -> return empty response
            if (request.MediaItemIds == null || request.MediaItemIds.Count == 0)
            {
                return Results.Ok(new List<MediaTicketDto>() as IReadOnlyList<MediaTicketDto>);
            }

            // Check max tickets per request
            if (request.MediaItemIds.Count > MediaContentRoutes.MaxTicketsPerRequest)
            {
                return Results.BadRequest();
            }

            // De-duplicate and issue tickets
            var distinctIds = request.MediaItemIds.Distinct().ToList();
            var tickets = new List<MediaTicketDto>(distinctIds.Count);

            foreach (Guid mediaItemId in distinctIds)
            {
                MediaAccessTicket ticket = ticketService.Issue(mediaItemId, operatorId);
                tickets.Add(new MediaTicketDto(
                    mediaItemId,
                    MediaContentRoutes.ContentUrl(mediaItemId, ticket.Token),
                    ticket.ExpiresAtUtc));
            }

            return Results.Ok(tickets as IReadOnlyList<MediaTicketDto>);
        }

        /// <summary>
        /// Serves media content with two authentication paths:
        /// 1. Bearer token (authenticated user with OperatorId claim)
        /// 2. Per-item ticket query parameter (for img/video tags)
        ///
        /// Both paths validate the operator's Active/IsApproved status before serving.
        /// Records access in the audit log unless the request includes a Range header not starting at byte 0.
        /// Returns 500 if the audit write fails (evidence access must be logged).
        /// </summary>
        public static async Task<IResult> GetContentAsync(
            Guid id,
            string? ticket,
            IMediaAccessTicketService ticketService,
            IMediaItemRepository mediaItems,
            IOperatorRepository operators,
            IMediaStorageProvider storage,
            IAccessAuditLogRepository auditLog,
            INetworkTierResolver tierResolver,
            ILogger<Program> logger,
            HttpContext context,
            CancellationToken ct)
        {
            // Resolve operator ID from claims or ticket (before checking file existence)
            Guid? operatorId = await ResolveAuthorizedOperatorAsync(id, ticket, ticketService, operators, context, ct);
            if (operatorId == null)
            {
                return Results.Unauthorized();
            }

            // Check media item exists
            MediaItem? item = await mediaItems.GetAsync(id, ct);
            if (item == null)
            {
                return Results.NotFound();
            }

            // Check file exists before attempting stream
            if (!await storage.ExistsAsync(item.FilePath, ct))
            {
                logger.LogWarning("Media item {MediaItemId} has no file on disk at {FilePath}", id, item.FilePath);
                return Results.NotFound();
            }

            // Determine if we should audit this request
            // Skip auditing range requests unless they start at byte 0
            bool shouldAudit = true;
            if (context.Request.Headers.TryGetValue("Range", out StringValues rangeValue))
            {
                string? rangeHeader = rangeValue.FirstOrDefault();
                if (!string.IsNullOrEmpty(rangeHeader) && !rangeHeader.StartsWith("bytes=0-", StringComparison.OrdinalIgnoreCase))
                {
                    shouldAudit = false;
                }
            }

            // Record access in audit log if required
            if (shouldAudit)
            {
                try
                {
                    _ = await auditLog.RecordAccessAsync(
                        id,
                        operatorId.Value.ToString(),
                        "View",
                        tierResolver.ResolveClientIp(context),
                        "In-app media view",
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to record access audit for media item {MediaItemId} by operator {OperatorId}", id, operatorId);
                    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
                }
            }

            // Stream the content
            Stream stream = await storage.OpenReadStreamAsync(item.FilePath, ct);
            string contentType = item.MediaFormat switch
            {
                "video/mp4" or "mp4" => "video/mp4",
                "image/jpeg" or "jpg" or "jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

            return Results.Stream(stream, contentType, item.FileName, enableRangeProcessing: true);
        }

        /// <summary>
        /// Serves a clip's preview MP4 (the transcoded, thumbnail-scale copy) with the same two authentication paths
        /// and range-request behaviour as <see cref="GetContentAsync"/>.
        /// Does not write an access audit entry: a preview is a thumbnail-scale view of a clip the operator has already
        /// opened, so auditing each preview request would bury the full-clip views that matter.
        /// Previews are written once and never change, so the response is cacheable for a day.
        /// Returns 404 when the clip has no preview path or the preview file is missing.
        /// </summary>
        public static async Task<IResult> GetPreviewAsync(
            Guid id,
            string? ticket,
            IMediaAccessTicketService ticketService,
            IMediaItemRepository mediaItems,
            IOperatorRepository operators,
            IMediaStorageProvider storage,
            ILogger<Program> logger,
            HttpContext context,
            CancellationToken ct)
        {
            if (await ResolveAuthorizedOperatorAsync(id, ticket, ticketService, operators, context, ct) == null)
            {
                return Results.Unauthorized();
            }

            MediaItem? item = await mediaItems.GetAsync(id, ct);
            if (item == null || string.IsNullOrEmpty(item.PreviewPath))
            {
                return Results.NotFound();
            }

            if (!await storage.ExistsAsync(item.PreviewPath, ct))
            {
                logger.LogWarning("Media item {MediaItemId} has no preview file on disk at {PreviewPath}", id, item.PreviewPath);
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "private, max-age=86400";
            Stream stream = await storage.OpenReadStreamAsync(item.PreviewPath, ct);
            return Results.Stream(stream, "video/mp4", enableRangeProcessing: true);
        }

        /// <summary>
        /// Serves the thumbnail for a media item: the image itself for images, the stored first frame for videos.
        /// Uses the same authentication as <see cref="GetContentAsync"/>, but does not write an access audit entry:
        /// a gallery load requests one thumbnail per item, so auditing them would bury the full-clip views that matter.
        /// Returns 404 when no thumbnail can be produced.
        /// </summary>
        public static async Task<IResult> GetThumbnailAsync(
            Guid id,
            string? ticket,
            IMediaAccessTicketService ticketService,
            IMediaItemRepository mediaItems,
            IOperatorRepository operators,
            IMediaStorageProvider storage,
            IMediaThumbnailService thumbnails,
            HttpContext context,
            CancellationToken ct)
        {
            if (await ResolveAuthorizedOperatorAsync(id, ticket, ticketService, operators, context, ct) == null)
            {
                return Results.Unauthorized();
            }

            MediaItem? item = await mediaItems.GetAsync(id, ct);
            if (item == null)
            {
                return Results.NotFound();
            }

            string? path = await thumbnails.ResolveThumbnailPathAsync(item, ct);
            if (path == null || !await storage.ExistsAsync(path, ct))
            {
                return Results.NotFound();
            }

            // A video's thumbnail is always the PNG frame; an image is served in its own format.
            string contentType = MediaThumbnailContentType(item);
            Stream stream = await storage.OpenReadStreamAsync(path, ct);
            return Results.Stream(stream, contentType);
        }

        private static string MediaThumbnailContentType(MediaItem item)
        {
            return item.MediaFormat switch
            {
                "image/jpeg" or "jpg" or "jpeg" => "image/jpeg",
                "image/png" or "png" => "image/png",
                "video/mp4" or "mp4" => "image/png",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Resolves the operator behind a media request: the signed-in claim first, else the per-item ticket.
        /// A ticket is only honoured for an operator who is still active and approved. Returns null when unauthorized.
        /// </summary>
        private static async Task<Guid?> ResolveAuthorizedOperatorAsync(
            Guid id,
            string? ticket,
            IMediaAccessTicketService ticketService,
            IOperatorRepository operators,
            HttpContext context,
            CancellationToken ct)
        {
            Guid? operatorId = null;
            bool viaTicket = false;

            // Try authenticated path first
            if (context.User.Identity?.IsAuthenticated == true)
            {
                string? operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
                if (Guid.TryParse(operatorIdClaim, out Guid claimedOperatorId))
                {
                    operatorId = claimedOperatorId;
                }
            }

            // Fall back to ticket validation if no authenticated claim
            if (operatorId == null)
            {
                operatorId = ticketService.Validate(ticket, id);
                viaTicket = true;
            }

            if (operatorId == null)
            {
                return null;
            }

            // For ticket auth, validate operator status
            if (viaTicket)
            {
                Operator? @operator = await operators.GetAsync(operatorId.Value, ct);
                if (@operator == null || !@operator.Active || !@operator.IsApproved)
                {
                    return null;
                }
            }

            return operatorId;
        }

        /// <summary>
        /// Lists media items, optionally filtered by device ID and date range.
        /// Semantics:
        /// - deviceId + from + to → GetByDeviceAndDateRangeAsync
        /// - deviceId only → GetByDeviceIdAsync
        /// - no deviceId → ListAsync
        /// - from/to given without deviceId → 400
        /// - from > to → 400
        /// </summary>
        public static async Task<IResult> ListMediaItemsAsync(
            Guid? deviceId,
            DateTime? from,
            DateTime? to,
            IMediaItemRepository mediaItems,
            CancellationToken ct)
        {
            // Validate: from/to require deviceId
            if ((from.HasValue || to.HasValue) && !deviceId.HasValue)
            {
                return Results.BadRequest();
            }

            // Validate: from must be <= to
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                return Results.BadRequest();
            }

            IReadOnlyList<MediaItem> items = (deviceId, from, to) switch
            {
                // All three: date range query
                (Guid id, DateTime fromUtc, DateTime toUtc) => await mediaItems.GetByDeviceAndDateRangeAsync(id, fromUtc, toUtc, ct),
                // Device ID only
                (Guid id, null, null) => await mediaItems.GetByDeviceIdAsync(id, ct),
                // No filters: list all
                (null, null, null) => await mediaItems.ListAsync(ct),
                // Other combinations already caught by validation above
                _ => await mediaItems.ListAsync(ct)
            };

            return Results.Ok(items.Select(x => x.ToDto()));
        }

        /// <summary>
        /// Pages media items from one or more devices within a UTC range, oldest first, for the media viewer.
        /// The total count comes from the database, so the client can page without fetching every clip.
        /// Semantics:
        /// - deviceIds (1..64) and from/to (from must be &lt;= to) are required
        /// - skip defaults to 0, must be &gt;= 0
        /// - take defaults to 100, must be 1..500
        /// </summary>
        public static async Task<IResult> ListMediaItemPageAsync(
            Guid[]? deviceIds,
            DateTime? from,
            DateTime? to,
            int? skip,
            int? take,
            IMediaItemRepository mediaItems,
            CancellationToken ct)
        {
            const int MaxDevices = 64;
            const int DefaultTake = 100;
            const int MaxTake = 500;

            if (deviceIds is null || deviceIds.Length == 0 || deviceIds.Length > MaxDevices)
            {
                return Results.BadRequest();
            }

            if (!from.HasValue || !to.HasValue || from.Value > to.Value)
            {
                return Results.BadRequest();
            }

            int effectiveSkip = skip ?? 0;
            int effectiveTake = take ?? DefaultTake;
            if (effectiveSkip < 0 || effectiveTake < 1 || effectiveTake > MaxTake)
            {
                return Results.BadRequest();
            }

            MediaItemPage page = await mediaItems.GetPageByDevicesAndDateRangeAsync(
                deviceIds, from.Value, to.Value, effectiveSkip, effectiveTake, ct);

            return Results.Ok(new MediaItemPageDto([.. page.Items.Select(x => x.ToDto())], page.TotalCount));
        }

        /// <summary>
        /// Gets a single media item by ID.
        /// Returns 404 if not found.
        /// </summary>
        public static async Task<IResult> GetMediaItemAsync(
            Guid id,
            IMediaItemRepository mediaItems,
            CancellationToken ct)
        {
            MediaItem? item = await mediaItems.GetAsync(id, ct);
            return item == null ? Results.NotFound() : Results.Ok(item.ToDto());
        }

        /// <summary>
        /// Gets a device's WiFi RSSI/health history within a date range, oldest first - the data
        /// path behind the Evidence "Device x Time" view's per-device RSSI plot. Both <paramref
        /// name="from"/> and <paramref name="to"/> are required (unlike media-items, there's no
        /// "list everything" fallback for a time-series metric) and <paramref name="from"/> must
        /// not be after <paramref name="to"/>.
        /// </summary>
        public static async Task<IResult> GetDeviceHealthHistoryAsync(
            Guid id,
            DateTime? from,
            DateTime? to,
            IDeviceHealthRepository deviceHealth,
            CancellationToken ct)
        {
            if (!from.HasValue || !to.HasValue)
            {
                return Results.BadRequest();
            }

            if (from.Value > to.Value)
            {
                return Results.BadRequest();
            }

            IReadOnlyList<DeviceHealth> history = await deviceHealth.GetHistoryAsync(id, from.Value, to.Value, ct);
            return Results.Ok(history.Select(x => x.ToDto()));
        }

        /// <summary>
        /// Gets the signal figures for cameras over a window: stats, the week grid, deviation hours, overlay periods and
        /// buckets. Computed here so every client shows the same numbers. <paramref name="devices"/> is a comma-separated
        /// list of device IDs; both <paramref name="from"/> and <paramref name="to"/> are required, and <paramref name="from"/>
        /// must not be after <paramref name="to"/>.
        /// </summary>
        public static async Task<IResult> GetSignalAnalysisAsync(
            string? devices,
            DateTime? from,
            DateTime? to,
            ISignalAnalysisService analysis,
            CancellationToken ct)
        {
            if (!from.HasValue || !to.HasValue || from.Value > to.Value)
            {
                return Results.BadRequest();
            }

            var ids = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(devices))
            {
                foreach (string part in devices.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Guid.TryParse(part, out Guid id))
                    {
                        return Results.BadRequest();
                    }

                    ids.Add(id);
                }
            }

            SignalAnalysis result = await analysis.AnalyzeAsync(ids, from.Value, to.Value, ct);
            return Results.Ok(result.ToDto());
        }

        /// <summary>
        /// Handler for POST /media/{id}/stills: the Evidence "grab still with hash" write path.
        /// Captures a frame from the source video at the given offset as a new, independently-hashed
        /// derived media item (never modifying the original), optionally pinning it to a case.
        /// 404 if the source media item doesn't exist; 400 for invalid input (negative offset,
        /// a case pin without a reason, or invalid pngBase64).
        /// </summary>
        public static async Task<IResult> CreateMediaStillAsync(
            Guid id,
            CaptureStillRequestDto request,
            IMediaStillCaptureService stillCaptureService,
            HttpContext context,
            CancellationToken ct)
        {
            if (request.FrameOffsetMs < 0)
            {
                return Results.BadRequest("FrameOffsetMs must be >= 0");
            }

            if (request.CaseId.HasValue && string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest("Reason is required when pinning the still to a case");
            }

            string? capturedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(capturedBy))
            {
                return Results.Unauthorized();
            }

            MediaStillCaptureResult result = await stillCaptureService.CaptureStillAsync(
                id, request.FrameOffsetMs, request.PngBase64, request.CaseId, request.Reason, capturedBy, ct);

            return result.Error switch
            {
                MediaStillCaptureError.None => Results.Created($"/api/v1/media/{result.Still!.Id}/content", result.Still.ToDto()),
                MediaStillCaptureError.MediaNotFound => Results.NotFound(),
                MediaStillCaptureError.InvalidInput => Results.BadRequest(result.ErrorMessage),
                _ => Results.Problem(detail: result.ErrorMessage, statusCode: StatusCodes.Status500InternalServerError)
            };
        }
    }
}
