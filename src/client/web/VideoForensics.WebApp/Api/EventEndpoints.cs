using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Event management and legal hold lifecycle control (plan §6/§7): listing events by various
    /// dimensions, and placing/releasing legal holds on media items to prevent/allow deletion.
    ///
    /// Per the plan's auth-default rule, EVERY endpoint here requires paired-device authentication
    /// (RequireAuthorization()) - there is no general exemption for read-only operations.
    ///
    /// LEGAL HOLD STEP-UP REASONING: Both placing AND releasing a legal hold are sensitive evidence-
    /// lifecycle actions, but RELEASE is the dangerous direction since it directly permits evidence
    /// deletion (removing the hold's protection). Accordingly, BOTH operations carry AddEndpointFilter
    /// (StepUpEndpointFilter) to force step-up re-authentication. Placing a hold is also step-upped
    /// for symmetry and to prevent accidental mass-holds via bulk/automated operations - an operator
    /// should actively re-authenticate for ANY hold-lifecycle change, not just release.
    /// </summary>
    public static class EventEndpoints
    {
        public static void MapEventEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/v1/events").RequireAuthorization();

            // Event read operations
            _ = group.MapGet("/", async (
                int? pageNumber,
                int? pageSize,
                IEventRepository events,
                CancellationToken ct) =>
            {
                // If both pageNumber and pageSize are provided, use paginated read
                if (pageNumber.HasValue && pageSize.HasValue)
                {
                    var paginatedResult = await events.ListPaginatedAsync(pageNumber.Value, pageSize.Value, ct);
                    var paginatedDto = new PaginatedResultDto<EventDto>(
                        Items: paginatedResult.Items.Select(x => x.ToDto()).ToList(),
                        TotalCount: paginatedResult.TotalCount,
                        PageNumber: paginatedResult.PageNumber,
                        PageSize: paginatedResult.PageSize
                    );
                    return Results.Ok(paginatedDto);
                }

                // Otherwise, return all events (backward compatibility with ListAsync)
                return Results.Ok((await events.ListAsync(ct)).Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("List all events")
                .WithDescription("Retrieves all events from all devices. Supports optional pagination via pageNumber and pageSize query parameters (both required for pagination).");

            _ = group.MapGet("/{id:guid}", async (Guid id, IEventRepository events, CancellationToken ct) =>
            {
                var @event = await events.GetAsync(id, ct);
                return @event == null ? Results.NotFound() : Results.Ok(@event.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get event by ID")
                .WithDescription("Retrieves a single event by its unique identifier.");

            _ = group.MapGet("/by-device/{deviceId:guid}", async (
                Guid deviceId,
                DateTime? fromUtc,
                DateTime? toUtc,
                IEventRepository events,
                CancellationToken ct) =>
            {
                DateTime from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
                DateTime to = toUtc ?? DateTime.UtcNow;
                var eventList = await events.ListByDeviceAndDateRangeAsync(deviceId, from, to, ct);
                return Results.Ok(eventList.Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("List events by device")
                .WithDescription("Retrieves events for a specific device within a date range (defaults to last 30 days).");

            _ = group.MapGet("/by-location/{locationId:guid}", async (
                Guid locationId,
                DateTime? fromUtc,
                DateTime? toUtc,
                IEventRepository events,
                CancellationToken ct) =>
            {
                DateTime from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
                DateTime to = toUtc ?? DateTime.UtcNow;
                var eventList = await events.ListByLocationAndDateRangeAsync(locationId, from, to, ct);
                return Results.Ok(eventList.Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("List events by location")
                .WithDescription("Retrieves events for all devices in a location within a date range (defaults to last 30 days).");

            _ = group.MapGet("/by-type/{eventType}", async (
                string eventType,
                Guid? deviceId,
                Guid? locationId,
                DateTime? fromUtc,
                DateTime? toUtc,
                IEventRepository events,
                CancellationToken ct) =>
            {
                DateTime from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
                DateTime to = toUtc ?? DateTime.UtcNow;
                var eventList = deviceId.HasValue
                    ? await events.ListByDeviceEventTypeAndDateRangeAsync(deviceId.Value, eventType, from, to, ct)
                    : locationId.HasValue
                        ? await events.ListByLocationEventTypeAndDateRangeAsync(locationId.Value, eventType, from, to, ct)
                        : new List<Event>();
                return Results.Ok(eventList.Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("List events by type")
                .WithDescription("Retrieves events of a specific type within a date range, filtered by device or location (defaults to last 30 days).");

            _ = group.MapGet("/summary/{locationId:guid}", async (
                Guid locationId,
                DateTime? fromUtc,
                DateTime? toUtc,
                IEventRepository events,
                CancellationToken ct) =>
            {
                DateTime from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
                DateTime to = toUtc ?? DateTime.UtcNow;
                var summary = await events.GetEventTypeSummaryAsync(locationId, from, to, ct);
                return Results.Ok(summary);
            })
                .RequireRateLimiting("media")
                .WithSummary("Get event type summary")
                .WithDescription("Retrieves a count of events by type for a location within a date range (defaults to last 30 days).");

            _ = group.MapGet("/unanswered/{deviceId:guid}", async (
                Guid deviceId,
                IEventRepository events,
                CancellationToken ct) =>
            {
                var eventList = await events.ListUnansweredOrFlaggedAsync(deviceId, ct);
                return Results.Ok(eventList.Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("List unanswered/flagged events")
                .WithDescription("Retrieves events marked as unanswered or flagged for review on a specific device.");

            // Event write operations
            _ = group.MapPost("/", async (
                EventDto dto,
                IEventRepository events,
                CancellationToken ct) =>
            {
                var entity = dto.ToDomain();
                var result = await events.UpsertAsync(entity, ct);
                return Results.Ok(result.ToDto());
            })
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Create or update event")
                .WithDescription("Creates a new event or updates an existing one (upsert).");

            _ = group.MapDelete("/{id:guid}", async (
                Guid id,
                IEventRepository events,
                CancellationToken ct) =>
            {
                await events.DeleteAsync(id, ct);
                return Results.NoContent();
            })
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Delete event")
                .WithDescription("Permanently deletes an event.");

            // Legal Hold operations
            var legalHoldGroup = app.MapGroup("/api/v1/legal-holds").RequireAuthorization();

            _ = legalHoldGroup.MapGet("/", async (
                string? mediaItemIds,
                ILegalHoldRepository legalHolds,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(mediaItemIds))
                {
                    return Results.BadRequest("mediaItemIds query parameter is required");
                }

                var ids = mediaItemIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out Guid id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                var holds = await legalHolds.GetActiveByMediaItemIdsAsync(ids, ct);
                return Results.Ok(holds.Select(x => x.ToDto()));
            })
                .RequireRateLimiting("media")
                .WithSummary("Get active legal holds")
                .WithDescription("Retrieves currently-active legal holds for the specified media items.");

            _ = legalHoldGroup.MapPost("/", async (
                PlaceLegalHoldRequestDto request,
                ILegalHoldRepository legalHolds,
                HttpContext context,
                CancellationToken ct) =>
            {
                var createdBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value ?? "system";
                var hold = await legalHolds.PlaceAsync(request.MediaItemId, request.Reason, createdBy, ct);
                return Results.Created($"/api/v1/legal-holds/{hold.Id}", hold.ToDto());
            })
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Place legal hold")
                .WithDescription("Places a legal hold on a media item to prevent retention-policy auto-deletion. Requires step-up authentication.");

            _ = legalHoldGroup.MapPost("/release", async (
                ReleaseLegalHoldRequestDto request,
                ILegalHoldRepository legalHolds,
                HttpContext context,
                CancellationToken ct) =>
            {
                var releasedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value ?? "system";
                await legalHolds.ReleaseAsync(request.LegalHoldId, releasedBy, request.ReleaseReason, ct);
                return Results.NoContent();
            })
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Release legal hold")
                .WithDescription("Releases an active legal hold on a media item, permitting retention-policy auto-deletion. Requires step-up authentication (sensitive operation).");
        }
    }
}
