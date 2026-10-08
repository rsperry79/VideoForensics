using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
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

        /// <inheritdoc />
        /// <remarks>
        /// Uses Chat Completions streaming (<c>stream: true</c>). Content deltas are yielded as they arrive. Tool-call
        /// arguments arrive as fragments keyed by <c>index</c>, so they are buffered per index and reported when the
        /// step completes. As with the Anthropic provider, a tool call takes precedence over text in the same step.
        /// </remarks>
        public async IAsyncEnumerable<LlmStreamEvent> StreamCompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<LlmToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty", nameof(messages));

            var baseUrl = _options.BaseUrl ?? "https://api.openai.com";
            var endpoint = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

            var request = BuildRequest(messages, tools);
            request.Stream = true;
            var content = JsonContent.Create(request, options: _jsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using Stream body = await response.Content.ReadAsStreamAsync(ct);

            var text = new StringBuilder();
            var toolCalls = new SortedDictionary<int, PartialToolCall>();

            await foreach (SseItem<string> sse in SseParser.Create(body).EnumerateAsync(ct))
            {
                // OpenAI terminates the stream with a literal [DONE] sentinel rather than a JSON event.
                if (sse.Data == "[DONE]")
                    break;

                using var doc = JsonDocument.Parse(sse.Data);
                if (!doc.RootElement.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0)
                    continue;

                if (!choices[0].TryGetProperty("delta", out JsonElement delta))
                    continue;

                if (delta.TryGetProperty("content", out JsonElement contentElement) && contentElement.ValueKind == JsonValueKind.String)
                {
                    string fragment = contentElement.GetString() ?? string.Empty;
                    if (fragment.Length > 0)
                    {
                        _ = text.Append(fragment);
                        yield return new LlmTextDelta(fragment);
                    }
                }

                if (delta.TryGetProperty("tool_calls", out JsonElement callsElement) && callsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement call in callsElement.EnumerateArray())
                    {
                        int index = call.TryGetProperty("index", out JsonElement indexElement) ? indexElement.GetInt32() : 0;
                        if (!toolCalls.TryGetValue(index, out PartialToolCall? partial))
                        {
                            partial = new PartialToolCall();
                            toolCalls[index] = partial;
                        }

                        if (call.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
                            partial.Id ??= idElement.GetString();

                        if (call.TryGetProperty("function", out JsonElement function))
                        {
                            if (function.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String)
                                partial.Name = nameElement.GetString() ?? string.Empty;

                            if (function.TryGetProperty("arguments", out JsonElement argsElement) && argsElement.ValueKind == JsonValueKind.String)
                                _ = partial.Arguments.Append(argsElement.GetString());
                        }
                    }
                }
            }

            if (toolCalls.Count > 0)
            {
                PartialToolCall first = toolCalls.First().Value;
                yield return new LlmStepCompleted(new LlmCompletionResult(
                    TextReply: null,
                    ToolCallId: first.Id,
                    ToolName: first.Name,
                    ToolArgumentsJson: first.Arguments.Length > 0 ? first.Arguments.ToString() : "{}"));
                yield break;
            }

            if (text.Length == 0)
                throw new InvalidOperationException("No valid content type found in OpenAI response");

            yield return new LlmStepCompleted(new LlmCompletionResult(
                TextReply: text.ToString(),
                ToolCallId: null,
                ToolName: null,
                ToolArgumentsJson: null));
        }

        /// <summary>Accumulates one streamed tool call across its fragments.</summary>
        private sealed class PartialToolCall
        {
            public string? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public StringBuilder Arguments { get; } = new();
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

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
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

    }
}
