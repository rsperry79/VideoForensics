using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.WebApp.Chat
{
    /// <summary>Implements ILlmChatProvider for OpenAI Chat Completions API and OpenAI-compatible endpoints (vLLM, Ollama, etc.)
    /// with tool-use support via function calling.</summary>
    public class OpenAiCompatibleChatProvider : ILlmChatProvider
    {
        private readonly OpenAiChatOptions _options;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public OpenAiCompatibleChatProvider(OpenAiChatOptions options, HttpClient httpClient)
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

            var baseUrl = _options.BaseUrl ?? "https://api.openai.com";
            var endpoint = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

            var request = BuildRequest(messages, tools);
            var content = JsonContent.Create(request, options: _jsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<OpenAiResponse>(_jsonOptions, cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty response from OpenAI API");

            return ParseResponse(responseBody);
        }

        private OpenAiRequest BuildRequest(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools)
        {
            var convertedMessages = messages
                .Select(msg => msg.Role switch
                {
                    "user" => new OpenAiMessage
                    {
                        Role = "user",
                        Content = msg.Content
                    },
                    "assistant" when msg.ToolName != null => new OpenAiMessage
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = new[]
                        {
                            new OpenAiToolCall
                            {
                                Id = msg.ToolCallId,
                                Type = "function",
                                Function = new OpenAiFunction
                                {
                                    Name = msg.ToolName,
                                    Arguments = msg.ToolArgumentsJson ?? "{}"
                                }
                            }
                        }
                    },
                    "assistant" => new OpenAiMessage
                    {
                        Role = "assistant",
                        Content = msg.Content
                    },
                    "tool" => new OpenAiMessage
                    {
                        Role = "tool",
                        Content = msg.Content,
                        ToolCallId = msg.ToolCallId
                    },
                    _ => throw new InvalidOperationException($"Unknown role: {msg.Role}")
                })
                .ToList();

            var toolDefinitions = tools
                .Select(tool => new OpenAiTool
                {
                    Type = "function",
                    Function = new OpenAiFunctionDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = ParseJsonToObject(tool.JsonSchema)
                    }
                })
                .ToList();

            return new OpenAiRequest
            {
                Model = _options.Model,
                Messages = convertedMessages,
                Tools = toolDefinitions.Count > 0 ? toolDefinitions : null
            };
        }

        private LlmCompletionResult ParseResponse(OpenAiResponse response)
        {
            if (response.Choices == null || response.Choices.Count == 0)
                throw new InvalidOperationException("Empty choices in OpenAI response");

            var choice = response.Choices[0];
            var message = choice.Message;

            if (message.ToolCalls != null && message.ToolCalls.Length > 0)
            {
                var toolCall = message.ToolCalls[0];
                return new LlmCompletionResult(
                    TextReply: null,
                    ToolCallId: toolCall.Id,
                    ToolName: toolCall.Function?.Name,
                    ToolArgumentsJson: toolCall.Function?.Arguments);
            }

            if (!string.IsNullOrEmpty(message.Content))
            {
                return new LlmCompletionResult(
                    TextReply: message.Content,
                    ToolCallId: null,
                    ToolName: null,
                    ToolArgumentsJson: null);
            }

            throw new InvalidOperationException("No valid content type found in OpenAI response");
        }

        private static object? ParseJsonToObject(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<object>(json);
        }

        // DTO classes for OpenAI API
        private class OpenAiRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("messages")]
            public List<OpenAiMessage> Messages { get; set; } = new();

            [JsonPropertyName("tools")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<OpenAiTool>? Tools { get; set; }
        }

        private class OpenAiMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Content { get; set; }

            [JsonPropertyName("tool_calls")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public OpenAiToolCall[]? ToolCalls { get; set; }

            [JsonPropertyName("tool_call_id")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? ToolCallId { get; set; }
        }

        private class OpenAiToolCall
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = "";

            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("function")]
            public OpenAiFunction? Function { get; set; }
        }

        private class OpenAiFunction
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("arguments")]
            public string Arguments { get; set; } = "{}";
        }

        private class OpenAiTool
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("function")]
            public OpenAiFunctionDefinition? Function { get; set; }
        }

        private class OpenAiFunctionDefinition
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("description")]
            public string Description { get; set; } = "";

            [JsonPropertyName("parameters")]
            public object? Parameters { get; set; }
        }

        private class OpenAiResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAiChoice>? Choices { get; set; }
        }

        private class OpenAiChoice
        {
            [JsonPropertyName("message")]
            public OpenAiMessage Message { get; set; } = new();
        }
    }
}
