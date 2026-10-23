using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
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

        /// <inheritdoc />
        /// <remarks>
        /// Uses Anthropic's server-sent events (<c>stream: true</c>). Text blocks are yielded as they arrive. Tool-use
        /// input arrives as partial JSON fragments, which are buffered until the step completes. If the model emits
        /// a tool call, the step reports that call (even if text came first), because the orchestrator can only act
        /// on one call at a time.
        /// </remarks>
        public async IAsyncEnumerable<LlmStreamEvent> StreamCompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<LlmToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty", nameof(messages));

            var baseUrl = _options.BaseUrl ?? "https://api.anthropic.com";
            var endpoint = $"{baseUrl.TrimEnd('/')}/v1/messages";

            var request = BuildRequest(messages, tools);
            request.Stream = true;
            var content = JsonContent.Create(request, options: _jsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            httpRequest.Headers.Add("x-api-key", _options.ApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Why: the response body carries the error reason (e.g. unknown model, invalid request) that
                // EnsureSuccessStatusCode discards. The API key is only sent in the request header, so it is never echoed here.
                string errorBody = await response.Content.ReadAsStringAsync(ct);
                if (errorBody.Length > 500)
                    errorBody = errorBody[..500];
                throw new HttpRequestException(
                    $"Anthropic API returned {(int)response.StatusCode} ({response.StatusCode}): {errorBody}", null, response.StatusCode);
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(ct);

            var text = new StringBuilder();
            string? toolCallId = null;
            string? toolName = null;
            var toolArgumentsJson = new StringBuilder();
            bool sawToolUse = false;

            await foreach (SseItem<string> sse in SseParser.Create(body).EnumerateAsync(ct))
            {
                switch (sse.EventType)
                {
                    case "content_block_start":
                    {
                        using var doc = JsonDocument.Parse(sse.Data);
                        JsonElement block = doc.RootElement.GetProperty("content_block");
                        if (block.GetProperty("type").GetString() == "tool_use")
                        {
                            sawToolUse = true;
                            toolCallId = block.GetProperty("id").GetString();
                            toolName = block.GetProperty("name").GetString();
                        }
                        break;
                    }

                    case "content_block_delta":
                    {
                        using var doc = JsonDocument.Parse(sse.Data);
                        JsonElement delta = doc.RootElement.GetProperty("delta");
                        switch (delta.GetProperty("type").GetString())
                        {
                            case "text_delta":
                                string fragment = delta.GetProperty("text").GetString() ?? string.Empty;
                                if (fragment.Length > 0)
                                {
                                    _ = text.Append(fragment);
                                    yield return new LlmTextDelta(fragment);
                                }
                                break;

                            case "input_json_delta":
                                _ = toolArgumentsJson.Append(delta.GetProperty("partial_json").GetString());
                                break;
                        }
                        break;
                    }

                    case "error":
                        throw new InvalidOperationException($"Anthropic stream error: {sse.Data}");

                    case "message_stop":
                        goto Done;
                }
            }

            Done:
            if (sawToolUse)
            {
                yield return new LlmStepCompleted(new LlmCompletionResult(
                    TextReply: null,
                    ToolCallId: toolCallId,
                    ToolName: toolName,
                    ToolArgumentsJson: toolArgumentsJson.Length > 0 ? toolArgumentsJson.ToString() : "{}"));
                yield break;
            }

            if (text.Length == 0)
                throw new InvalidOperationException("No valid content type found in Anthropic response");

            yield return new LlmStepCompleted(new LlmCompletionResult(
                TextReply: text.ToString(),
                ToolCallId: null,
                ToolName: null,
                ToolArgumentsJson: null));
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

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
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
    }
}
