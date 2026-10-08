namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Sends a message to the embedded MCP-backed chat assistant and gets a reply. The server-side
    /// implementation drives an LLM tool-use loop against this app's own MCP tools; a Remote*
    /// implementation (client hosts) proxies this over HTTP using VideoForensics.Api.Contracts DTOs,
    /// following the same pattern as every other Remote* client (see IDeviceRepository / RemoteDeviceRepository).
    /// </summary>
    public interface IChatService
    {
        /// <summary>Sends a new user message (with prior conversation history) and returns the assistant's reply.</summary>
        Task<ChatTurnResult> SendMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct);

        /// <summary>
        /// Sends a new user message and streams the turn as it happens: text deltas as the LLM produces them,
        /// tool invocations as they start, and a final <see cref="ChatTurnCompleted"/> carrying the full result.
        /// The final event is always <see cref="ChatTurnCompleted"/>.
        /// </summary>
        IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct);
    }

    /// <summary>A single turn in a chat conversation.</summary>
    public class ChatTurn
    {
        /// <summary>"user" or "assistant".</summary>
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>The assistant's reply to a chat turn, plus which MCP tools (if any) it invoked.</summary>
    public class ChatTurnResult
    {
        public string Reply { get; set; } = string.Empty;
        public IReadOnlyList<string> ToolsInvoked { get; set; } = [];
    }

    /// <summary>One event in a streamed chat turn. See <see cref="IChatService.StreamMessageAsync"/>.</summary>
    public abstract record ChatStreamEvent;

    /// <summary>A fragment of assistant text, in arrival order. Concatenating all deltas gives the streamed text.</summary>
    /// <param name="Text">The text fragment (may be a partial word or sentence).</param>
    public sealed record ChatTextDelta(string Text) : ChatStreamEvent;

    /// <summary>The assistant has started invoking the named MCP tool.</summary>
    /// <param name="ToolName">MCP tool name.</param>
    public sealed record ChatToolInvoked(string ToolName) : ChatStreamEvent;

    /// <summary>The turn is finished. Always the last event; carries the authoritative reply and tools invoked.</summary>
    /// <param name="Result">Final reply and tool list (matches what <see cref="IChatService.SendMessageAsync"/> returns).</param>
    public sealed record ChatTurnCompleted(ChatTurnResult Result) : ChatStreamEvent;
}
