namespace VideoForensics.WebApp.Chat
{
    /// <summary>Configuration for AnthropicChatProvider.</summary>
    public class AnthropicChatOptions
    {
        /// <summary>Anthropic API key.</summary>
        public required string ApiKey { get; init; }

        /// <summary>Model ID (e.g., "claude-3-5-sonnet-20241022").</summary>
        public required string Model { get; init; }

        /// <summary>Optional base URL for custom/self-hosted Anthropic-compatible endpoints. Defaults to api.anthropic.com.</summary>
        public string? BaseUrl { get; init; }
    }

    /// <summary>Configuration for OpenAiCompatibleChatProvider.</summary>
    public class OpenAiChatOptions
    {
        /// <summary>API key for OpenAI or OpenAI-compatible endpoint.</summary>
        public required string ApiKey { get; init; }

        /// <summary>Model ID (e.g., "gpt-4", or model name for self-hosted).</summary>
        public required string Model { get; init; }

        /// <summary>Optional base URL for self-hosted OpenAI-compatible endpoints (e.g., vLLM, Ollama). Defaults to api.openai.com.</summary>
        public string? BaseUrl { get; init; }
    }
}
