using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Server log viewer API endpoints: history query and live streaming for SuperAdmin users on local tier.
    /// Requires SuperAdminLocal authorization + step-up token validation.
    /// </summary>
    public static class LogEndpoints
    {
        private const int DefaultLimit = 500;
        private const int MaxLimit = 2000;
        private const int MinLimit = 1;
        private const int HeartbeatIntervalSeconds = 15;

        private static readonly string[] ValidLogLevels = { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };

        public static void MapLogEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/logs").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            _ = group.MapGet("/", GetLogsAsync).WithName("GetLogs").AddEndpointFilter<StepUpEndpointFilter>();
            _ = group.MapGet("/stream", StreamLogsAsync).WithName("StreamLogs").AddEndpointFilter<StepUpEndpointFilter>();
        }

        /// <summary>
        /// Returns a paginated snapshot of log entries matching the query filters.
        /// </summary>
        public static async Task<IResult> GetLogsAsync(
            [AsParameters] LogQueryDto query,
            InMemoryLogBuffer buffer,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate minLevel if provided
            if (!string.IsNullOrEmpty(query.MinLevel) && !ValidLogLevels.Contains(query.MinLevel, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid log level",
                    Detail = $"MinLevel must be one of: {string.Join(", ", ValidLogLevels)}"
                });
            }

            // Clamp limit to valid range
            int clampedLimit = Math.Max(MinLimit, Math.Min(query.Limit, MaxLimit));
            if (clampedLimit == 0)
                clampedLimit = DefaultLimit;

            // Get snapshot from buffer
            var snapshot = buffer.GetSnapshot(query.MinLevel, query.Search, query.AfterSequence, clampedLimit + 1);

            // Check if truncated (more results available than limit)
            bool truncated = snapshot.Count > clampedLimit;
            var entries = snapshot.Take(clampedLimit).Select(r => r.ToDto()).ToList();

            long latestSequence = buffer.LatestSequence;

            // Log the view operation
            string auditDetails = $"minLevel: {query.MinLevel ?? "none"}, limit: {clampedLimit}";
            Guid? operatorId = ExtractOperatorId(context);
            await auditLog.LogAsync(
                SecurityAuditEventTypes.LogViewed,
                operatorId,
                null,
                tierResolver.ResolveClientIp(context),
                auditDetails,
                isUrgent: false,
                ct);

            var pageDto = new LogPageDto(entries, latestSequence, truncated);
            return Results.Ok(pageDto);
        }

        /// <summary>
        /// Streams log entries as Server-Sent Events, supporting reconnection via Last-Event-ID header.
        /// Emits heartbeat comments every 15 seconds when idle to keep the connection alive.
        /// </summary>
        public static async Task<IResult> StreamLogsAsync(
            string? minLevel,
            string? search,
            int limit = DefaultLimit,
            InMemoryLogBuffer? buffer = null,
            ISecurityAuditLogger? auditLog = null,
            INetworkTierResolver? tierResolver = null,
            HttpContext? context = null,
            CancellationToken ct = default)
        {
            // Validate minLevel if provided
            if (!string.IsNullOrEmpty(minLevel) && !ValidLogLevels.Contains(minLevel, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Invalid log level" });
            }

            // Clamp limit
            int clampedLimit = Math.Max(MinLimit, Math.Min(limit, MaxLimit));
            if (clampedLimit == 0)
                clampedLimit = DefaultLimit;

            // Get Last-Event-ID from header for reconnection
            long? afterSequence = null;
            if (context?.Request.Headers.TryGetValue("Last-Event-ID", out var lastEventId) == true)
            {
                if (long.TryParse(lastEventId.ToString(), out long parsedId))
                {
                    afterSequence = parsedId;
                }
            }

            // Log the stream open
            if (auditLog != null && context != null && tierResolver != null)
            {
                string auditDetails = $"minLevel: {minLevel ?? "none"}, stream";
                Guid? operatorId = ExtractOperatorId(context);
                await auditLog.LogAsync(
                    SecurityAuditEventTypes.LogViewed,
                    operatorId,
                    null,
                    tierResolver.ResolveClientIp(context),
                    auditDetails,
                    isUrgent: false,
                    ct);
            }

            // Return SSE response
            return Results.ServerSentEvents(CreateEventStream(buffer, minLevel, search, afterSequence, ct));
        }

        private static async IAsyncEnumerable<string> CreateEventStream(
            InMemoryLogBuffer? buffer,
            string? minLevel,
            string? search,
            long? afterSequence,
            [EnumeratorCancellation] CancellationToken ct)
        {
            // Send initial backlog
            if (buffer != null)
            {
                var backlog = buffer.GetSnapshot(minLevel, search, afterSequence, 1);
                foreach (var record in backlog)
                {
                    var dto = record.ToDto();
                    yield return $"id: {record.Sequence}\ndata: {JsonSerializer.Serialize(dto)}\n";
                }
            }

            // Set up heartbeat interval for keep-alive
            using (var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(HeartbeatIntervalSeconds)))
            {
                while (true)
                {
                    try
                    {
                        if (!await heartbeatTimer.WaitForNextTickAsync(ct))
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Send a comment as heartbeat to keep connection alive
                    yield return ": heartbeat\n";
                }
            }
        }

        private static Guid? ExtractOperatorId(HttpContext context)
        {
            var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (!string.IsNullOrEmpty(operatorIdClaim) && Guid.TryParse(operatorIdClaim, out Guid operatorId))
            {
                return operatorId;
            }
            return null;
        }
    }
}