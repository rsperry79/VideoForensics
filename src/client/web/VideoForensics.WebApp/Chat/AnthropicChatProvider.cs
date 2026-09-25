using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.WebApp.Chat
{
    /// <summary>Implements ILlmChatProvider for Anthropic's Messages API with tool use support.
    /// Supports both api.anthropic.com and self-hosted/compatible endpoints via custom BaseUrl.</summary>
    public class AnthropicChatProvider : ILlmChatProvider
    {
        private readonly AnthropicChatOptions _options;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public AnthropicChatProvider(AnthropicChatOptions options, HttpClient httpClient)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken ct)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty", nameof(messages));

            var baseUrl = _options.BaseUrl ?? "https://api.anthropic.com";
            var endpoint = $"{baseUrl.TrimEnd('/')}/v1/messages";

            var request = BuildRequest(messages, tools);
            var content = JsonContent.Create(request, options: _jsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            httpRequest.Headers.Add("x-api-key", _options.ApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<AnthropicResponse>(_jsonOptions, cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty response from Anthropic API");

            return ParseResponse(responseBody);
        }

        private AnthropicRequest BuildRequest(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools)
        {
            var convertedMessages = messages
                .Select(msg => msg.Role switch
                {
                    "user" => new AnthropicMessage
                    {
                        Role = "user",
                        Content = new[] { new AnthropicContent { Type = "text", Text = msg.Content } }
                    },
                    "assistant" when msg.ToolName != null => new AnthropicMessage
                    {
                        Role = "assistant",
                        Content = new[]
                        {
                            new AnthropicContent
                            {
                                Type = "tool_use",
                                Id = msg.ToolCallId,
                                Name = msg.ToolName,
                                Input = ParseJsonToObject(msg.ToolArgumentsJson)
                            }
                        }
                    },
                    "assistant" => new AnthropicMessage
                    {
                        Role = "assistant",
                        Content = new[] { new AnthropicContent { Type = "text", Text = msg.Content } }
                    },
                    "tool" => new AnthropicMessage
                    {
                        Role = "user",
                        Content = new[]
                        {
                            new AnthropicContent
                            {
                                Type = "tool_result",
                                ToolUseId = msg.ToolCallId,
                                Content = msg.Content
                            }
                        }
                    },
                    _ => throw new InvalidOperationException($"Unknown role: {msg.Role}")
                })
                .ToList();

            var toolDefinitions = tools
                .Select(tool => new AnthropicTool
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = ParseJsonToObject(tool.JsonSchema)
                })
                .ToList();

            return new AnthropicRequest
            {
                Model = _options.Model,
                MaxTokens = 4096,
                Messages = convertedMessages,
                Tools = toolDefinitions.Count > 0 ? toolDefinitions : null
            };
        }

        private LlmCompletionResult ParseResponse(AnthropicResponse response)
        {
            if (response.Content == null || response.Content.Count == 0)
                throw new InvalidOperationException("Empty content in Anthropic response");

            foreach (var content in response.Content)
            {
                if (content.Type == "text" && content.Text != null)
                {
                    return new LlmCompletionResult(
                        TextReply: content.Text,
                        ToolCallId: null,
                        ToolName: null,
                        ToolArgumentsJson: null);
                }

                if (content.Type == "tool_use" && content.Name != null)
                {
                    var inputJson = content.Input != null
                        ? JsonSerializer.Serialize(content.Input, _jsonOptions)
                        : "{}";

                    return new LlmCompletionResult(
                        TextReply: null,
                        ToolCallId: content.Id,
                        ToolName: content.Name,
                        ToolArgumentsJson: inputJson);
                }
            }

            throw new InvalidOperationException("No valid content type found in Anthropic response");
        }

        private static object? ParseJsonToObject(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<object>(json);
        }

        // DTO classes for Anthropic API
        private class AnthropicRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("messages")]
            public List<AnthropicMessage> Messages { get; set; } = new();

            [JsonPropertyName("tools")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<AnthropicTool>? Tools { get; set; }
        }

        private class AnthropicMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public AnthropicContent[]? Content { get; set; }
        }

        private class AnthropicContent
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("text")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Text { get; set; }

            [JsonPropertyName("id")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Name { get; set; }

            [JsonPropertyName("input")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object? Input { get; set; }

            [JsonPropertyName("tool_use_id")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? ToolUseId { get; set; }

            [JsonPropertyName("content")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Content { get; set; }
        }

        private class AnthropicTool
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("description")]
            public string Description { get; set; } = "";

            [JsonPropertyName("input_schema")]
            public object? InputSchema { get; set; }
        }

        private class AnthropicResponse
        {
            [JsonPropertyName("content")]
            public List<AnthropicContent>? Content { get; set; }
        }
    }
}
