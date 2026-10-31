using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
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
        /// <summary>
        /// Pages events from one or more devices within a UTC range, newest first, for the review page's Events tab.
        /// Semantics:
        /// - deviceIds (1..64) and from/to (from must be &lt;= to) are required
        /// - skip defaults to 0, must be &gt;= 0
        /// - take defaults to 100, must be 1..500
        /// - excludeTypes optionally drops events whose type exactly matches an entry
        /// </summary>
        public static async Task<IResult> ListEventPageAsync(
            Guid[]? deviceIds,
            DateTime? from,
            DateTime? to,
            string[]? excludeTypes,
            int? skip,
            int? take,
            IEventRepository events,
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

            EventPage page = await events.ListPageByDevicesAndDateRangeAsync(
                deviceIds, from.Value, to.Value, excludeTypes, effectiveSkip, effectiveTake, ct);

            return Results.Ok(new EventPageDto([.. page.Items.Select(x => x.ToDto())], page.TotalCount));
        }

        /// <summary>
        /// Returns the detection rows for up to 200 events in one call, for the review page's Detail tab.
        /// Events with no detail rows are omitted. eventIds is required and must hold 1..200 ids.
        /// </summary>
        public static async Task<IResult> GetEventDetailsAsync(
            Guid[]? eventIds,
            IEventDetailRepository details,
            CancellationToken ct)
        {
            const int MaxEventIds = 200;

            if (eventIds is null || eventIds.Length == 0 || eventIds.Length > MaxEventIds)
            {
                return Results.BadRequest();
            }

            IReadOnlyList<EventDetail> found = await details.GetDetailsAsync(eventIds, ct);
            IReadOnlyList<EventDetailDto> dtos = [.. found.Select(d => d.ToDto())];
            return Results.Ok(dtos);
        }

        public static void MapEventEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/events").RequireAuthorization();

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
                    PaginatedResult<Event> paginatedResult = await events.ListPaginatedAsync(pageNumber.Value, pageSize.Value, ct);
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

            _ = group.MapGet("/page", ListEventPageAsync)
                .RequireRateLimiting("media")
                .WithSummary("Page events")
                .WithDescription("Pages events from one or more devices within a UTC range, newest first, for the review page's Events tab. deviceIds (1..64) and from/to (from <= to) are required; skip defaults to 0 (>= 0); take defaults to 100 (1..500); excludeTypes optionally drops event types.");

            _ = group.MapGet("/details", GetEventDetailsAsync)
                .RequireRateLimiting("media")
                .WithSummary("Get event details")
                .WithDescription("Returns the detection rows (detection, occurrences, zones, people, alerts) for up to 200 events, for the review page's Detail tab. Events with no detail rows are omitted.");

            _ = group.MapGet("/{id:guid}", async (Guid id, IEventRepository events, CancellationToken ct) =>
            {
                Event? @event = await events.GetAsync(id, ct);
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
                IReadOnlyList<Event> eventList = await events.ListByDeviceAndDateRangeAsync(deviceId, from, to, ct);
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
                IReadOnlyList<Event> eventList = await events.ListByLocationAndDateRangeAsync(locationId, from, to, ct);
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
                IReadOnlyList<Event> eventList = deviceId.HasValue
                    ? await events.ListByDeviceEventTypeAndDateRangeAsync(deviceId.Value, eventType, from, to, ct)
                    : locationId.HasValue
                        ? await events.ListByLocationEventTypeAndDateRangeAsync(locationId.Value, eventType, from, to, ct)
                        : [];
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
                Dictionary<string, int> summary = await events.GetEventTypeSummaryAsync(locationId, from, to, ct);
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
                IReadOnlyList<Event> eventList = await events.ListUnansweredOrFlaggedAsync(deviceId, ct);
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
                Event entity = dto.ToDomain();
                Event result = await events.UpsertAsync(entity, ct);
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
            RouteGroupBuilder legalHoldGroup = app.MapGroup("/api/v1/legal-holds").RequireAuthorization();

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

                IReadOnlyList<LegalHold> holds = await legalHolds.GetActiveByMediaItemIdsAsync(ids, ct);
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
                string createdBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value ?? "system";
                LegalHold hold = await legalHolds.PlaceAsync(request.MediaItemId, request.Reason, createdBy, ct);
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
                string releasedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value ?? "system";
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
