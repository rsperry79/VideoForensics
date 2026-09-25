using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
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

            _ = group.MapGet("/media-items/{id:guid}", GetMediaItemAsync)
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
                var ticket = ticketService.Issue(mediaItemId, operatorId);
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

            // No valid auth path
            if (operatorId == null)
            {
                return Results.Unauthorized();
            }

            // For ticket auth, validate operator status
            if (viaTicket)
            {
                var @operator = await operators.GetAsync(operatorId.Value, ct);
                if (@operator == null || !@operator.Active || !@operator.IsApproved)
                {
                    return Results.Unauthorized();
                }
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
            if (context.Request.Headers.TryGetValue("Range", out var rangeValue))
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
                    await auditLog.RecordAccessAsync(
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
        /// Gets a single media item by ID.
        /// Returns 404 if not found.
        /// </summary>
        public static async Task<IResult> GetMediaItemAsync(
            Guid id,
            IMediaItemRepository mediaItems,
            CancellationToken ct)
        {
            MediaItem? item = await mediaItems.GetAsync(id, ct);
            if (item == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(item.ToDto());
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
    }
}
