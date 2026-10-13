namespace VideoForensics.WebApp.Chat
{
    /// <summary>A message in the LLM-facing conversation. Richer than the public ChatTurn DTO/domain type:
    /// includes tool-call and tool-result turns needed to drive a tool-use loop.</summary>
    public record LlmMessage(string Role, string? Content, string? ToolCallId = null, string? ToolName = null, string? ToolArgumentsJson = null);
    // Role is one of: "user", "assistant", "tool"

    /// <summary>Describes one MCP tool the LLM may call, in provider-agnostic form.</summary>
    public record LlmToolDefinition(string Name, string Description, string JsonSchema);
    // JsonSchema is the tool's parameters schema as a JSON string (object schema, e.g. {"type":"object","properties":{...}})

    /// <summary>Result of one LLM completion step: either a final text reply, or a request to call one tool (not both).</summary>
    public record LlmCompletionResult(string? TextReply, string? ToolCallId, string? ToolName, string? ToolArgumentsJson)
    {
        public bool IsToolCall => ToolName is not null;
    }

    /// <summary>One event from a streamed LLM completion step.</summary>
    public abstract record LlmStreamEvent;

    /// <summary>A text fragment produced by the model, in arrival order.</summary>
    public sealed record LlmTextDelta(string Text) : LlmStreamEvent;

    /// <summary>The final result of the step. Always the last event of a stream; emitted exactly once.</summary>
    public sealed record LlmStepCompleted(LlmCompletionResult Result) : LlmStreamEvent;

    /// <summary>Abstraction over a chat-completion LLM backend that supports tool use (Anthropic Messages API,
    /// or an OpenAI-compatible Chat Completions API). Implementations are stateless per call.</summary>
    public interface ILlmChatProvider
    {
        /// <summary>
        /// Runs one completion step, streaming text as the model produces it. The stream ends with exactly one
        /// <see cref="LlmStepCompleted"/> carrying the step's final text or tool call.
        /// </summary>
        IAsyncEnumerable<LlmStreamEvent> StreamCompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, CancellationToken ct);
    }
}
