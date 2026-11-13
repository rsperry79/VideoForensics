using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
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
