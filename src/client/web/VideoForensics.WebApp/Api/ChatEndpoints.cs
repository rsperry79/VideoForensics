using System.Text.Json;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Chat API endpoints for the embedded MCP-backed assistant (Milestone 8).
    /// Operators send messages to the assistant, which drives an MCP tool-use loop
    /// against this app's forensic analysis tools and returns replies + tools invoked.
    /// </summary>
    public static class ChatEndpoints
    {
        /// <summary>Wire format for SSE payloads: camelCase, matching the default web JSON used elsewhere in the API.</summary>
        private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

        public static void MapChatEndpoints(this WebApplication app)
        {
            _ = app.MapPost("/api/v1/chat", SendChatMessageAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .WithSummary("Send a message to the MCP-backed chat assistant")
                .Produces<ChatResponseDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status500InternalServerError);

            _ = app.MapPost("/api/v1/chat/stream", StreamChatMessageAsync)
                .RequireAuthorization(VideoForensicsPolicies.ReadOnly)
                .WithSummary("Stream a reply from the MCP-backed chat assistant as server-sent events")
                .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden);
        }

        private static async Task<IResult> SendChatMessageAsync(
            ChatRequestDto request,
            IChatService chatService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            string? validationError = ValidateRequest(request);
            if (validationError != null)
            {
                return Results.BadRequest((object)new { error = validationError });
            }

            try
            {
                // Convert DTOs to domain entities
                var history = (request.History ?? Array.Empty<ChatMessageDto>())
                    .Select(h => h.ToDomain())
                    .ToList();

                // Invoke the orchestrator
                var result = await chatService.SendMessageAsync(history, request.Message, ct);

                // Log the interaction (Info on success, tools invoked)
                logger.LogInformation(
                    "Chat: message from operator, tools_invoked={ToolsInvoked}",
                    string.Join(",", result.ToolsInvoked));

                // Return response as DTO
                return Results.Ok(result.ToDto());
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Chat: request cancelled");
                return Results.StatusCode(StatusCodes.Status408RequestTimeout);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chat: unhandled exception");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Streams a chat turn as server-sent events. Frames are <c>delta</c> (text fragment), <c>tool</c> (tool
        /// invocation started), and a final <c>done</c> carrying the full <see cref="ChatResponseDto"/>. Each frame is
        /// flushed as soon as it is written so the client renders text while the model is still generating.
        /// </summary>
        internal static async Task StreamChatMessageAsync(
            ChatRequestDto request,
            IChatService chatService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            string? validationError = ValidateRequest(request);
            if (validationError != null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = validationError }, ct);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            // Tells reverse proxies such as nginx not to buffer the stream, which would defeat incremental delivery.
            context.Response.Headers["X-Accel-Buffering"] = "no";

            var history = (request.History ?? Array.Empty<ChatMessageDto>())
                .Select(h => h.ToDomain())
                .ToList();

            try
            {
                await context.Response.StartAsync(ct);

                await foreach (ChatStreamEvent evt in chatService.StreamMessageAsync(history, request.Message, ct))
                {
                    (string eventName, string data) = ToSseFrame(evt);
                    await context.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);

                    if (evt is ChatTurnCompleted completed)
                    {
                        logger.LogInformation(
                            "Chat: streamed message from operator, tools_invoked={ToolsInvoked}",
                            string.Join(",", completed.Result.ToolsInvoked));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected mid-stream. Nothing to send; the response is already committed.
                logger.LogWarning("Chat: streamed request cancelled");
            }
            catch (Exception ex)
            {
                // Headers are already sent, so the status code cannot change. Log and let the stream end.
                logger.LogError(ex, "Chat: unhandled exception while streaming");
            }
        }

        /// <summary>Maps a streamed event to its SSE event name and JSON data payload.</summary>
        internal static (string EventName, string Data) ToSseFrame(ChatStreamEvent evt) => evt switch
        {
            ChatTextDelta delta => ("delta", JsonSerializer.Serialize(new ChatStreamDeltaDto(delta.Text), SseJsonOptions)),
            ChatToolInvoked tool => ("tool", JsonSerializer.Serialize(new ChatStreamToolDto(tool.ToolName), SseJsonOptions)),
            ChatTurnCompleted completed => ("done", JsonSerializer.Serialize(completed.Result.ToDto(), SseJsonOptions)),
            _ => throw new InvalidOperationException($"Unknown chat stream event: {evt.GetType().Name}")
        };

        /// <summary>Returns an error message for an invalid request, or null when the request is acceptable.</summary>
        private static string? ValidateRequest(ChatRequestDto? request)
        {
            if (request == null)
            {
                return "Request body is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return "Message cannot be empty.";
            }

            // Limit message and history size
            if (request.Message.Length > 10_000)
            {
                return "Message is too long (max 10,000 characters).";
            }

            if (request.History?.Count > 100)
            {
                return "History is too long (max 100 turns).";
            }

            return null;
        }
    }
}
