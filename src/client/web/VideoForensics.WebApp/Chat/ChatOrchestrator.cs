using ModelContextProtocol.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Infrastructure;

namespace VideoForensics.WebApp.Chat
{
    /// <summary>
    /// Server-side implementation of IChatService that drives an LLM tool-use loop against
    /// this app's own MCP tools. Constructs the appropriate ILlmChatProvider based on
    /// configuration, connects an McpClient to the local /mcp endpoint, and orchestrates
    /// the tool-use loop.
    /// </summary>
    public class ChatOrchestrator : IChatService
    {
        private readonly ILlmApiKeyStore _keyStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChatOrchestrator> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly Func<string, string, string?, ILlmChatProvider>? _providerFactory;
        private readonly Func<Task<IMcpToolClient>>? _mcpToolClientFactory;

        private const int MaxToolCalls = 5;

        /// <summary>
        /// Main constructor for production use - uses real provider factories.
        /// </summary>
        public ChatOrchestrator(
            ILlmApiKeyStore keyStore,
            IHttpClientFactory httpClientFactory,
            ILogger<ChatOrchestrator> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _providerFactory = null;
            _mcpToolClientFactory = null;
        }

        /// <summary>
        /// Test constructor - allows injection of mock provider and MCP tool client factories.
        /// </summary>
        internal ChatOrchestrator(
            ILlmApiKeyStore keyStoreMock,
            IHttpClientFactory httpClientFactory,
            ILogger<ChatOrchestrator> logger,
            Func<string, string, string?, ILlmChatProvider> providerFactory,
            Func<Task<IMcpToolClient>> mcpToolClientFactory)
        {
            _keyStore = keyStoreMock;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _httpContextAccessor = null!;
            _providerFactory = providerFactory;
            _mcpToolClientFactory = mcpToolClientFactory;
        }

        public async Task<ChatTurnResult> SendMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
        {
            try
            {
                // Ensure message is not empty
                if (string.IsNullOrWhiteSpace(message))
                {
                    return new ChatTurnResult
                    {
                        Reply = "Please provide a message.",
                        ToolsInvoked = Array.Empty<string>()
                    };
                }

                // Get LLM configuration
                string? provider = await _keyStore.GetProviderAsync(ct);
                string? apiKey = await _keyStore.GetDecryptedApiKeyAsync(ct);
                string? model = await _keyStore.GetModelAsync(ct);

                if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
                {
                    _logger.LogWarning("ChatOrchestrator: LLM not configured (provider={Provider}, apiKey={HasKey}, model={Model})",
                        provider, !string.IsNullOrEmpty(apiKey), model);
                    return new ChatTurnResult
                    {
                        Reply = "The chat assistant is not configured yet. Please configure an LLM provider in Settings.",
                        ToolsInvoked = Array.Empty<string>()
                    };
                }

                string? baseUrl = await _keyStore.GetBaseUrlAsync(ct);

                // Construct the appropriate LLM provider
                ILlmChatProvider llmProvider = _providerFactory?.Invoke(provider, apiKey, baseUrl)
                    ?? ConstructLlmProvider(provider, apiKey, model, baseUrl);

                // Get IMcpToolClient (either from factory, or create one from McpClient)
                IMcpToolClient mcpToolClient = await (_mcpToolClientFactory?.Invoke()
                    ?? GetDefaultMcpToolClientAsync(ct));

                try
                {
                    // List available MCP tools
                    var toolsParams = new ModelContextProtocol.Protocol.ListToolsRequestParams();
                    var toolsResult = await mcpToolClient.ListToolsAsync(toolsParams, ct);
                    var tools = new List<LlmToolDefinition>();
                    if (toolsResult?.Tools != null)
                    {
                        foreach (ModelContextProtocol.Protocol.Tool tool in toolsResult.Tools)
                        {
                            tools.Add(new LlmToolDefinition(
                                Name: tool.Name,
                                Description: tool.Description ?? string.Empty,
                                JsonSchema: tool.InputSchema.ValueKind != JsonValueKind.Undefined ? JsonSerializer.Serialize(tool.InputSchema) : "{}"));
                        }
                    }

                    // Build LLM message history
                    var llmMessages = new List<LlmMessage>();
                    foreach (var turn in history)
                    {
                        llmMessages.Add(new LlmMessage(turn.Role, turn.Content));
                    }
                    llmMessages.Add(new LlmMessage("user", message));

                    // Tool-use loop
                    var toolsInvoked = new List<string>();
                    int toolCallCount = 0;

                    while (toolCallCount < MaxToolCalls)
                    {
                        // Call LLM
                        var completion = await llmProvider.CompleteAsync(llmMessages, tools, ct);

                        if (!completion.IsToolCall)
                        {
                            // Final text reply - return it
                            return new ChatTurnResult
                            {
                                Reply = completion.TextReply ?? "I couldn't generate a response.",
                                ToolsInvoked = toolsInvoked
                            };
                        }

                        // Tool call - invoke it and continue loop
                        if (string.IsNullOrEmpty(completion.ToolName))
                        {
                            _logger.LogWarning("ChatOrchestrator: LLM returned IsToolCall=true but ToolName is null");
                            return new ChatTurnResult
                            {
                                Reply = "An error occurred while processing your request.",
                                ToolsInvoked = toolsInvoked
                            };
                        }

                        _logger.LogInformation("ChatOrchestrator: Invoking tool {ToolName}", completion.ToolName);
                        toolsInvoked.Add(completion.ToolName);

                        // Invoke the MCP tool
                        IReadOnlyDictionary<string, object?>? toolArguments = null;
                        if (!string.IsNullOrEmpty(completion.ToolArgumentsJson))
                        {
                            var jsonElement = JsonSerializer.Deserialize<JsonElement>(completion.ToolArgumentsJson);
                            toolArguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonElement.GetRawText());
                        }

                        ModelContextProtocol.Protocol.CallToolResult toolResult = await mcpToolClient.CallToolAsync(completion.ToolName, toolArguments, ct);
                        string toolResultText = ExtractToolResultText(toolResult);

                        // Add assistant response and tool result to message history
                        llmMessages.Add(new LlmMessage(
                            Role: "assistant",
                            Content: null,
                            ToolCallId: completion.ToolCallId,
                            ToolName: completion.ToolName,
                            ToolArgumentsJson: completion.ToolArgumentsJson));

                        llmMessages.Add(new LlmMessage(
                            Role: "tool",
                            Content: toolResultText,
                            ToolCallId: completion.ToolCallId));

                        toolCallCount++;
                    }

                    // Max tool calls exceeded
                    _logger.LogWarning("ChatOrchestrator: Max tool calls ({MaxToolCalls}) exceeded", MaxToolCalls);
                    return new ChatTurnResult
                    {
                        Reply = "The assistant reached the maximum number of tool invocations. Please try again with a simpler request.",
                        ToolsInvoked = toolsInvoked
                    };
                }
                finally
                {
                    // Dispose if mcpToolClient wraps a real McpClient
                    (mcpToolClient as IDisposable)?.Dispose();
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "ChatOrchestrator: Request cancelled");
                return new ChatTurnResult
                {
                    Reply = "The request was cancelled.",
                    ToolsInvoked = Array.Empty<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatOrchestrator: Unhandled exception");
                return new ChatTurnResult
                {
                    Reply = "An error occurred while processing your request.",
                    ToolsInvoked = Array.Empty<string>()
                };
            }
        }

        private ILlmChatProvider ConstructLlmProvider(string provider, string apiKey, string model, string? baseUrl)
        {
            var httpClient = _httpClientFactory.CreateClient();

            return provider switch
            {
                "Anthropic" => new AnthropicChatProvider(
                    new AnthropicChatOptions { ApiKey = apiKey, Model = model, BaseUrl = baseUrl },
                    httpClient),

                "OpenAiCompatible" => new OpenAiCompatibleChatProvider(
                    new OpenAiChatOptions { ApiKey = apiKey, Model = model, BaseUrl = baseUrl },
                    httpClient),

                _ => throw new InvalidOperationException($"Unknown LLM provider: {provider}")
            };
        }

        private async Task<IMcpToolClient> GetDefaultMcpToolClientAsync(CancellationToken ct)
        {
            McpClient mcpClient = await ConnectToLocalMcpEndpointAsync(ct);
            return new McpToolClientAdapter(mcpClient);
        }

        private async Task<McpClient> ConnectToLocalMcpEndpointAsync(CancellationToken ct)
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Determine the local server's base address from configuration
            int port = ServerAddressResolver.ResolveConfiguredPort(_configuration);
            httpClient.BaseAddress = new Uri($"https://localhost:{port}");

            // Forward the current request's authorization header to the local MCP call
            if (_httpContextAccessor?.HttpContext?.Request.Headers.TryGetValue("Authorization", out var authHeader) == true)
            {
                httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader.ToString());
            }

            var mcpTransportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress, "/mcp")
            };

            var transport = new HttpClientTransport(mcpTransportOptions, httpClient, null);
            return await McpClient.CreateAsync(transport, null, null, ct);
        }

        private static string ExtractToolResultText(ModelContextProtocol.Protocol.CallToolResult toolResult)
        {
            if (toolResult?.Content == null || toolResult.Content.Count == 0)
            {
                return toolResult?.IsError == true ? "Tool failed" : "Tool succeeded with no output";
            }

            // Extract text content from tool result
            var textContents = toolResult.Content
                .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
                .Select(tc => tc.Text)
                .Where(t => !string.IsNullOrEmpty(t));

            return textContents.Any() ? string.Join("\n", textContents) : "Tool executed";
        }
    }
}
