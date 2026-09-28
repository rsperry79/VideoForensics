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
        }

        private static async Task<IResult> SendChatMessageAsync(
            ChatRequestDto request,
            IChatService chatService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            // Validate request
            if (request == null)
            {
                return Results.BadRequest(new { error = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message cannot be empty." });
            }

            // Limit message and history size
            if (request.Message.Length > 10_000)
            {
                return Results.BadRequest(new { error = "Message is too long (max 10,000 characters)." });
            }

            if (request.History?.Count > 100)
            {
                return Results.BadRequest(new { error = "History is too long (max 100 turns)." });
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
    }
}
