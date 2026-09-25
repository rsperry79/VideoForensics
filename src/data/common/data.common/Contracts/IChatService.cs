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
}
