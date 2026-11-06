using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Query string for the flag list, count and neighbour routes. Lists are comma-separated names; <c>fromUtc</c>/<c>toUtc</c>
    /// bound the flag window start, inclusive. <c>skip</c>/<c>take</c> and <c>direction</c> apply to the routes that use them.
    /// </summary>
    public sealed record MediaFlagQueryParameters(
        string? Types,
        string? Statuses,
        string? DeviceIds,
        DateTime? FromUtc,
        DateTime? ToUtc,
        int? Skip,
        int? Take,
        string? Direction);

    /// <summary>
    /// Media review flags over <c>/api/v1/media-flags</c>: reads for any signed-in role, keep and dismiss for Review and above,
    /// re-open for Admin only. The operator is always the signed-in token's, never a request field. Every successful change is
    /// published through <see cref="IMediaFlagChangeNotifier"/>, so open review pages reload.
    /// </summary>
    public static class MediaFlagEndpoints
    {
        private const int DefaultTake = 100;
        private const int MaxTake = 500;

        public static void MapMediaFlagEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/media-flags");

            _ = group.MapGet("", ListAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("count", CountAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("{id:guid}", GetAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("{id:guid}/neighbour", NeighbourAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapGet("{id:guid}/position", PositionAsync)
                .RequireAuthorization()
                .RequireRateLimiting("media");

            _ = group.MapPost("{id:guid}/status", SetStatusAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("media");

            _ = group.MapPost("{id:guid}/reopen", ReopenAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media");
        }

        /// <summary>Lists one page of flags matching the filter, newest first. Skip defaults to 0; take defaults to 100 and is 1..500.</summary>
        public static async Task<IResult> ListAsync(
            [AsParameters] MediaFlagQueryParameters query,
            IMediaFlagRepository flags,
            CancellationToken ct)
        {
            MediaFlagFilter? filter = TryBuildFilter(query);
            if (filter == null)
            {
                return Results.BadRequest();
            }

            int skip = query.Skip ?? 0;
            int take = query.Take ?? DefaultTake;
            if (skip < 0 || take < 1 || take > MaxTake)
            {
                return Results.BadRequest();
            }

            MediaFlagPage page = await flags.ListPageAsync(filter, skip, take, ct);
            return Results.Ok(page.ToDto());
        }

        /// <summary>Counts the flags matching the filter.</summary>
        public static async Task<IResult> CountAsync(
            [AsParameters] MediaFlagQueryParameters query,
            IMediaFlagRepository flags,
            CancellationToken ct)
        {
            MediaFlagFilter? filter = TryBuildFilter(query);
            if (filter == null)
            {
                return Results.BadRequest();
            }

            int count = await flags.CountAsync(filter, ct);
            return Results.Ok(count);
        }

        /// <summary>Gets one flag, or 404.</summary>
        public static async Task<IResult> GetAsync(Guid id, IMediaFlagRepository flags, CancellationToken ct)
        {
            MediaFlag? flag = await flags.GetAsync(id, ct);
            return flag == null ? Results.NotFound() : Results.Ok(flag.ToDto());
        }

        /// <summary>
        /// Gets the flag before (<c>Previous</c>) or after (<c>Next</c>) this one in the filtered list. 204 when it is at the end
        /// of the list, 404 when the flag itself does not exist, 400 for a missing direction or a bad filter.
        /// </summary>
        public static async Task<IResult> NeighbourAsync(
            Guid id,
            [AsParameters] MediaFlagQueryParameters query,
            IMediaFlagRepository flags,
            CancellationToken ct)
        {
            if (!Enum.TryParse(query.Direction, out MediaFlagNeighbour direction) || !Enum.IsDefined(direction))
            {
                return Results.BadRequest();
            }

            MediaFlagFilter? filter = TryBuildFilter(query);
            if (filter == null)
            {
                return Results.BadRequest();
            }

            if (await flags.GetAsync(id, ct) == null)
            {
                return Results.NotFound();
            }

            MediaFlag? neighbour = await flags.GetNeighbourAsync(id, filter, direction, ct);
            return neighbour == null ? Results.NoContent() : Results.Ok(neighbour.ToDto());
        }

        /// <summary>
        /// Gets the flag's 1-based position in the filtered list. 404 when the flag is missing or not in the filtered list,
        /// 400 for a bad filter.
        /// </summary>
        public static async Task<IResult> PositionAsync(
            Guid id,
            [AsParameters] MediaFlagQueryParameters query,
            IMediaFlagRepository flags,
            CancellationToken ct)
        {
            MediaFlagFilter? filter = TryBuildFilter(query);
            if (filter == null)
            {
                return Results.BadRequest();
            }

            int? position = await flags.GetPositionAsync(id, filter, ct);
            return position is { } value ? Results.Ok(value) : Results.NotFound();
        }

        /// <summary>
        /// Keeps or dismisses a flag (Review and above). Compare-and-set: 409 with the reviewer when the flag already moved on.
        /// Moving a flag back to Unreviewed is the reopen route, so it is 400 here. Publishes the change on success.
        /// </summary>
        public static async Task<IResult> SetStatusAsync(
            Guid id,
            SetFlagStatusRequestDto request,
            IMediaFlagRepository flags,
            IMediaFlagChangeNotifier notifier,
            HttpContext context,
            CancellationToken ct)
        {
            string? operatorId = OperatorIdOf(context);
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return Results.Unauthorized();
            }

            if (!TryParseStatus(request.ExpectedStatus, out MediaFlagStatus expected)
                || !TryParseStatus(request.NewStatus, out MediaFlagStatus next)
                || next == MediaFlagStatus.Unreviewed)
            {
                return Results.BadRequest();
            }

            try
            {
                MediaFlag flag = await flags.UpdateStatusAsync(id, expected, next, request.Reason, operatorId, ct);
                await notifier.MediaFlagChangedAsync(flag.Id, flag.DeviceId, ct);
                return Results.Ok(flag.ToDto());
            }
            catch (FlagStatusConflictException ex)
            {
                return Results.Conflict(ex.ToDto());
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Re-opens a dismissed flag as Unreviewed (Admin only). 409 when it is not dismissed. Publishes the change on success.</summary>
        public static async Task<IResult> ReopenAsync(
            Guid id,
            ReopenFlagRequestDto request,
            IMediaFlagRepository flags,
            IMediaFlagChangeNotifier notifier,
            HttpContext context,
            CancellationToken ct)
        {
            string? operatorId = OperatorIdOf(context);
            if (string.IsNullOrWhiteSpace(operatorId))
            {
                return Results.Unauthorized();
            }

            try
            {
                MediaFlag flag = await flags.ReopenAsync(id, operatorId, request.Reason, ct);
                await notifier.MediaFlagChangedAsync(flag.Id, flag.DeviceId, ct);
                return Results.Ok(flag.ToDto());
            }
            catch (FlagStatusConflictException ex)
            {
                return Results.Conflict(ex.ToDto());
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>The signed-in operator's id from the token. Null when the token carries none.</summary>
        private static string? OperatorIdOf(HttpContext context)
        {
            return context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
        }

        /// <summary>Builds the domain filter from the query, or null when a value is malformed or the window is inverted.</summary>
        private static MediaFlagFilter? TryBuildFilter(MediaFlagQueryParameters query)
        {
            if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
            {
                return null;
            }

            IReadOnlyList<Guid>? deviceIds = null;
            if (!string.IsNullOrWhiteSpace(query.DeviceIds))
            {
                var ids = new List<Guid>();
                foreach (string part in Split(query.DeviceIds))
                {
                    if (!Guid.TryParse(part, out Guid id))
                    {
                        return null;
                    }

                    ids.Add(id);
                }

                deviceIds = ids;
            }

            try
            {
                // The mapping rejects unknown enum names, so a bad type or status is a 400 without a repository call.
                return new MediaFlagFilterDto(
                    Split(query.Types),
                    Split(query.Statuses),
                    deviceIds,
                    query.FromUtc,
                    query.ToUtc).ToDomain();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static IReadOnlyList<string>? Split(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static bool TryParseStatus(string value, out MediaFlagStatus status)
        {
            return Enum.TryParse(value, ignoreCase: false, out status) && Enum.IsDefined(status);
        }
    }
}
