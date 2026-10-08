using System.Globalization;
using System.Buffers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>Tunables for the log SSE stream.</summary>
    public sealed class LogStreamOptions
    {
        /// <summary>Idle interval between keep-alive comment frames.</summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Server log viewer API endpoints: history query and live streaming for SuperAdmin users on local tier.
    /// Requires SuperAdminLocal authorization + step-up token validation.
    /// </summary>
    public static class LogEndpoints
    {
        private const int DefaultLimit = 500;
        private const int MaxLimit = 2000;

        private static readonly string[] ValidLogLevels = { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

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
            if (!IsValidMinLevel(query.MinLevel))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid log level",
                    Detail = $"MinLevel must be one of: {string.Join(", ", ValidLogLevels)}"
                });
            }

            int clampedLimit = ClampLimit(query.Limit);

            // Fetch one extra entry so truncation can be reported without a second pass.
            var snapshot = buffer.GetSnapshot(NullIfEmpty(query.MinLevel), NullIfEmpty(query.Search), query.AfterSequence, clampedLimit + 1);

            bool truncated = snapshot.Count > clampedLimit;
            var entries = snapshot.Take(clampedLimit).Select(r => r.ToDto()).ToList();

            long latestSequence = buffer.LatestSequence;

            // Audit details deliberately omit the search text: it can contain log content.
            await auditLog.LogAsync(
                SecurityAuditEventTypes.LogViewed,
                ExtractOperatorId(context),
                null,
                tierResolver.ResolveClientIp(context),
                $"minLevel: {query.MinLevel ?? "none"}, limit: {clampedLimit}",
                isUrgent: false,
                ct);

            return Results.Ok(new LogPageDto(entries, latestSequence, truncated));
        }

        /// <summary>
        /// Streams log entries as Server-Sent Events (backlog, then live), supporting reconnection via the
        /// Last-Event-ID header or <c>afterSequence</c>. Idle streams emit a keep-alive comment frame.
        /// </summary>
        public static async Task<IResult> StreamLogsAsync(
            string? minLevel,
            string? search,
            long? afterSequence,
            int? limit,
            InMemoryLogBuffer buffer,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            IOptions<LogStreamOptions> streamOptions,
            HttpContext context,
            CancellationToken ct)
        {
            if (!IsValidMinLevel(minLevel))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid log level",
                    Detail = $"MinLevel must be one of: {string.Join(", ", ValidLogLevels)}"
                });
            }

            int clampedLimit = ClampLimit(limit ?? 0);

            // Last-Event-ID is what a reconnecting EventSource sends; it wins over the query value.
            if (context.Request.Headers.TryGetValue("Last-Event-ID", out var lastEventId)
                && long.TryParse(lastEventId.ToString(), out long parsedId))
            {
                afterSequence = parsedId;
            }

            await auditLog.LogAsync(
                SecurityAuditEventTypes.LogViewed,
                ExtractOperatorId(context),
                null,
                tierResolver.ResolveClientIp(context),
                $"minLevel: {minLevel ?? "none"}, stream",
                isUrgent: false,
                ct);

            var (backlog, live) = buffer.SubscribeWithBacklog(NullIfEmpty(minLevel), NullIfEmpty(search), afterSequence, clampedLimit, ct);
            return new LogStreamResult(backlog, live, streamOptions.Value.HeartbeatInterval, ct);
        }

        private static bool IsValidMinLevel(string? minLevel)
            => string.IsNullOrEmpty(minLevel) || ValidLogLevels.Contains(minLevel, StringComparer.OrdinalIgnoreCase);

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>Omitted, zero or negative limits mean "use the default"; anything above the maximum is capped.</summary>
        private static int ClampLimit(int limit) => limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        private static Guid? ExtractOperatorId(HttpContext context)
        {
            var operatorIdClaim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return Guid.TryParse(operatorIdClaim, out Guid operatorId) ? operatorId : null;
        }

        /// <summary>
        /// Writes the SSE response by hand because Results.ServerSentEvents cannot emit comment frames,
        /// which are the standard keep-alive; data frames still go through SseFormatter (id + data lines).
        /// </summary>
        private sealed class LogStreamResult : IResult
        {
            private static readonly byte[] HeartbeatFrame = ": heartbeat\n\n"u8.ToArray();

            private readonly IReadOnlyList<LogRecord> _backlog;
            private readonly IAsyncEnumerable<LogRecord> _live;
            private readonly TimeSpan _heartbeat;
            private readonly CancellationToken _ct;

            public LogStreamResult(IReadOnlyList<LogRecord> backlog, IAsyncEnumerable<LogRecord> live, TimeSpan heartbeat, CancellationToken ct)
            {
                _backlog = backlog;
                _live = live;
                _heartbeat = heartbeat;
                _ct = ct;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                HttpResponse response = httpContext.Response;
                response.ContentType = "text/event-stream";
                response.Headers.CacheControl = "no-cache,no-store";
                httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

                try
                {
                    // Flush headers right away so the client sees the open stream even when nothing is logged.
                    await response.Body.FlushAsync(_ct);

                    foreach (LogRecord record in _backlog)
                    {
                        await WriteRecordAsync(response.Body, record, _ct);
                    }

                    await using IAsyncEnumerator<LogRecord> live = _live.GetAsyncEnumerator(_ct);
                    Task<bool> next = live.MoveNextAsync().AsTask();
                    while (true)
                    {
                        try
                        {
                            if (!await next.WaitAsync(_heartbeat, _ct))
                                break;
                        }
                        catch (TimeoutException)
                        {
                            await response.Body.WriteAsync(HeartbeatFrame, _ct);
                            await response.Body.FlushAsync(_ct);
                            continue;
                        }

                        await WriteRecordAsync(response.Body, live.Current, _ct);
                        next = live.MoveNextAsync().AsTask();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Client went away; disposing the enumerator above unsubscribes from the buffer.
                }
            }

            private static async Task WriteRecordAsync(Stream body, LogRecord record, CancellationToken ct)
            {
                var item = new SseItem<LogEntryDto>(record.ToDto()) { EventId = record.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                await SseFormatter.WriteAsync(
                    SingleAsync(item),
                    body,
                    static (i, writer) => WriteJson(i.Data, writer),
                    ct);
                await body.FlushAsync(ct);
            }

            private static void WriteJson(LogEntryDto dto, IBufferWriter<byte> writer)
            {
                using var json = new Utf8JsonWriter(writer);
                JsonSerializer.Serialize(json, dto, SseJson);
            }

            private static async IAsyncEnumerable<SseItem<LogEntryDto>> SingleAsync(SseItem<LogEntryDto> item)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }
    }
}