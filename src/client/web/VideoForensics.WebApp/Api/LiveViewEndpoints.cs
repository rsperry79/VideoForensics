using Microsoft.AspNetCore.Mvc;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Live-view session REST API: lets client hosts drive <see cref="ILiveViewSessionService"/> through
    /// <c>RemoteLiveViewSessionService</c>. Reads need <see cref="VideoForensicsPolicies.ReadOnly"/>; every
    /// mutation needs <see cref="VideoForensicsPolicies.Admin"/> because it starts, holds or tears down a
    /// camera stream.
    ///
    /// The acting operator is always taken from the <see cref="VideoForensicsClaimTypes.OperatorId"/> claim,
    /// never from the request body. A mutation with a missing or unparseable claim is a 401.
    ///
    /// Service exceptions map to statuses: <see cref="KeyNotFoundException"/> 404,
    /// <see cref="InvalidOperationException"/> 409 (live view disabled or invalid state),
    /// <see cref="NotSupportedException"/> 501 (provider has no live-view capability). Anything else is not
    /// caught and surfaces as a 500 via the normal exception handler.
    /// </summary>
    public static class LiveViewEndpoints
    {
        /// <summary>Maps the <c>/api/v1/live-view</c> routes onto the application.</summary>
        public static void MapLiveViewEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/live-view").RequireAuthorization();

            _ = group.MapPost("/start", StartAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Start live view")
                .WithDescription("Starts a live-view session for a device, or returns the existing active session (idempotent). The operator comes from the auth context.");

            _ = group.MapPost("/{sessionId:guid}/extend", ExtendAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Extend live view")
                .WithDescription("Extends the idle timeout of an active live-view session.");

            _ = group.MapPost("/{sessionId:guid}/promote", PromoteAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Promote live view to sustained")
                .WithDescription("Promotes a live-view session to sustained mode. The body is the human-readable promotion reason.");

            _ = group.MapPost("/{sessionId:guid}/demote", DemoteAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Demote live view from sustained")
                .WithDescription("Returns a sustained live-view session to active mode.");

            _ = group.MapPost("/{sessionId:guid}/stop", StopAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("media")
                .WithSummary("Stop live view")
                .WithDescription("Stops a live-view session. Idempotent if the session is already stopped.");

            _ = group.MapGet("/device/{deviceId:guid}/active", GetActiveAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .WithSummary("Get active live-view session")
                .WithDescription("Returns the active live-view session for a device, or 404 when none exists.");
        }

        /// <summary>Handler for POST /start</summary>
        public static async Task<IResult> StartAsync(
            StartLiveViewRequestDto request,
            ILiveViewSessionService liveView,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryGetOperatorId(context, out Guid operatorId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(request.Reason)
                || !Enum.TryParse(request.Reason, ignoreCase: true, out LiveViewTriggerReason reason)
                || !Enum.IsDefined(reason))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                LiveViewSession session = await liveView.StartAsync(request.DeviceId, reason, operatorId, ct);
                return Results.Ok(session.ToDto());
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>Handler for POST /{sessionId}/extend</summary>
        public static async Task<IResult> ExtendAsync(
            Guid sessionId,
            ILiveViewSessionService liveView,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryGetOperatorId(context, out _))
            {
                return Results.Unauthorized();
            }

            try
            {
                await liveView.ExtendAsync(sessionId, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>Handler for POST /{sessionId}/promote</summary>
        public static async Task<IResult> PromoteAsync(
            Guid sessionId,
            [FromBody] string reason,
            ILiveViewSessionService liveView,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryGetOperatorId(context, out _))
            {
                return Results.Unauthorized();
            }

            try
            {
                LiveViewSession session = await liveView.PromoteToSustainedAsync(sessionId, reason, ct);
                return Results.Ok(session.ToDto());
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>Handler for POST /{sessionId}/demote</summary>
        public static async Task<IResult> DemoteAsync(
            Guid sessionId,
            ILiveViewSessionService liveView,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryGetOperatorId(context, out _))
            {
                return Results.Unauthorized();
            }

            try
            {
                LiveViewSession session = await liveView.DemoteFromSustainedAsync(sessionId, ct);
                return Results.Ok(session.ToDto());
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>Handler for POST /{sessionId}/stop</summary>
        public static async Task<IResult> StopAsync(
            Guid sessionId,
            StopLiveViewRequestDto request,
            ILiveViewSessionService liveView,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryGetOperatorId(context, out _))
            {
                return Results.Unauthorized();
            }

            try
            {
                await liveView.StopAsync(sessionId, request.StopReason, ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>Handler for GET /device/{deviceId}/active</summary>
        public static async Task<IResult> GetActiveAsync(
            Guid deviceId,
            ILiveViewSessionService liveView,
            CancellationToken ct)
        {
            try
            {
                LiveViewSession? session = await liveView.GetActiveSessionAsync(deviceId, ct);
                return session == null ? Results.NotFound() : Results.Ok(session.ToDto());
            }
            catch (Exception ex) when (TryMapFailure(ex) is { } failure)
            {
                return failure;
            }
        }

        /// <summary>
        /// Reads the operator id from the auth claim. Returns false when the claim is missing or is not a GUID,
        /// which the caller turns into a 401.
        /// </summary>
        private static bool TryGetOperatorId(HttpContext context, out Guid operatorId)
        {
            string? claim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return Guid.TryParse(claim, out operatorId);
        }

        /// <summary>
        /// Maps the documented service exceptions to their status. Returns null for anything else so the
        /// exception filter lets it propagate unchanged.
        /// </summary>
        private static IResult? TryMapFailure(Exception ex) => ex switch
        {
            KeyNotFoundException => Results.Problem(statusCode: StatusCodes.Status404NotFound),
            InvalidOperationException => Results.Problem(statusCode: StatusCodes.Status409Conflict),
            NotSupportedException => Results.Problem(statusCode: StatusCodes.Status501NotImplemented),
            _ => null
        };
    }
}
