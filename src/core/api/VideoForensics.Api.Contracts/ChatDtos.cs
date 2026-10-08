namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// A single message in a chat conversation, for the embedded MCP-backed assistant.
    /// </summary>
    /// <param name="Role">The role of the message sender: "user" or "assistant".</param>
    /// <param name="Content">The message content.</param>
    public record ChatMessageDto(
        string Role,
        string Content
    );

    /// <summary>
    /// Request to send a new message to the chat assistant, with prior conversation history.
    /// </summary>
    /// <param name="History">Prior conversation history, ordered chronologically from oldest to newest.</param>
    /// <param name="Message">The new user message to send.</param>
    public record ChatRequestDto(
        IReadOnlyList<ChatMessageDto> History,
        string Message
    );

    /// <summary>
    /// The assistant's reply, plus which MCP tools (if any) it invoked to answer.
    /// </summary>
    /// <param name="Reply">The assistant's reply text.</param>
    /// <param name="ToolsInvoked">List of MCP tool names that were invoked to generate this reply, if any.</param>
    public record ChatResponseDto(
        string Reply,
        IReadOnlyList<string> ToolsInvoked
    );

    /// <summary>
    /// Payload of a "delta" frame on the streaming chat endpoint: a fragment of assistant text.
    /// </summary>
    /// <param name="Text">The text fragment.</param>
    public record ChatStreamDeltaDto(string Text);

    /// <summary>
    /// Payload of a "tool" frame on the streaming chat endpoint: the assistant started invoking an MCP tool.
    /// </summary>
    /// <param name="Name">MCP tool name.</param>
    public record ChatStreamToolDto(string Name);

    /// <summary>
    /// Request to update LLM settings (provider, model, API key, base URL).
    /// Only non-null/non-empty apiKey fields update the stored API key; null/empty leaves it unchanged.
    /// </summary>
    /// <param name="Provider">The LLM provider name (e.g., "Anthropic" or "OpenAiCompatible").</param>
    /// <param name="Model">The LLM model name (e.g., "claude-3-sonnet-20240229").</param>
    /// <param name="ApiKey">The API key (plain text on request); null/empty skips the update. Required on first set.</param>
    /// <param name="BaseUrl">The base URL for OpenAI-compatible endpoints (nullable).</param>
    public record LlmSettingsRequestDto(
        string Provider,
        string Model,
        string? ApiKey,
        string? BaseUrl
    );

    /// <summary>
    /// Current LLM configuration (provider, model, base URL). Does NOT include the API key.
    /// </summary>
    /// <param name="Provider">The LLM provider name (e.g., "Anthropic" or "OpenAiCompatible").</param>
    /// <param name="Model">The LLM model name (e.g., "claude-3-sonnet-20240229").</param>
    /// <param name="BaseUrl">The base URL for OpenAI-compatible endpoints, or empty if not set.</param>
    public record LlmSettingsResponseDto(
        string Provider,
        string Model,
        string BaseUrl
    );

    /// <summary>Extension methods for mapping chat entities to/from ChatDtos.</summary>
    public static class ChatDtoMapping
    {
        /// <summary>
        /// Converts a ChatTurn entity to a ChatMessageDto.
        /// </summary>
        public static ChatMessageDto ToDto(this VideoForensics.Data.Common.Contracts.ChatTurn entity)
        {
            return new ChatMessageDto(
                Role: entity.Role,
                Content: entity.Content
            );
        }

        /// <summary>
        /// Converts a ChatMessageDto to a ChatTurn entity.
        /// </summary>
        public static VideoForensics.Data.Common.Contracts.ChatTurn ToDomain(this ChatMessageDto dto)
        {
            return new VideoForensics.Data.Common.Contracts.ChatTurn
            {
                Role = dto.Role,
                Content = dto.Content
            };
        }

        /// <summary>
        /// Converts a ChatResponseDto to a ChatTurnResult.
        /// </summary>
        public static VideoForensics.Data.Common.Contracts.ChatTurnResult ToDomain(this ChatResponseDto dto)
        {
            return new VideoForensics.Data.Common.Contracts.ChatTurnResult
            {
                Reply = dto.Reply,
                ToolsInvoked = dto.ToolsInvoked
            };
        }

        /// <summary>
        /// Converts a ChatTurnResult to a ChatResponseDto.
        /// </summary>
        public static ChatResponseDto ToDto(this VideoForensics.Data.Common.Contracts.ChatTurnResult entity)
        {
            return new ChatResponseDto(
                Reply: entity.Reply,
                ToolsInvoked: entity.ToolsInvoked
            );
        }
    }
}
