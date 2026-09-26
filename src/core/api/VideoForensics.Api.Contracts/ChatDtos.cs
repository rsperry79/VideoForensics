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
