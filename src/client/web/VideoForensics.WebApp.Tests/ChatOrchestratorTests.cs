using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Chat;
using Xunit;

namespace VideoForensics.WebApp.Tests;

/// <summary>Tests for ChatOrchestrator - the server-side MCP chat loop orchestrator.</summary>
public class ChatOrchestrator_Tests
{
    private static readonly ChatTurn[] EmptyHistory = Array.Empty<ChatTurn>();

    private static LlmMessage UserMessage(string content) => new("user", content);
    private static LlmMessage AssistantMessage(string content) => new("assistant", content);
    private static LlmMessage ToolResultMessage(string toolCallId, string result) => new("tool", result, ToolCallId: toolCallId);

    /// <summary>Test double for IMcpToolClient - simple implementation for testing.</summary>
    private class TestMcpToolClient : IMcpToolClient
    {
        private readonly Func<ListToolsRequestParams, CancellationToken, ValueTask<ListToolsResult>>? _listToolsAsyncImpl;
        private readonly Func<string, IReadOnlyDictionary<string, object?>?, CancellationToken, ValueTask<CallToolResult>>? _callToolAsyncImpl;

        public TestMcpToolClient(
            Func<ListToolsRequestParams, CancellationToken, ValueTask<ListToolsResult>>? listToolsAsyncImpl = null,
            Func<string, IReadOnlyDictionary<string, object?>?, CancellationToken, ValueTask<CallToolResult>>? callToolAsyncImpl = null)
        {
            _listToolsAsyncImpl = listToolsAsyncImpl;
            _callToolAsyncImpl = callToolAsyncImpl;
        }

        public ValueTask<ListToolsResult> ListToolsAsync(ListToolsRequestParams request, CancellationToken ct)
        {
            if (_listToolsAsyncImpl != null)
                return _listToolsAsyncImpl(request, ct);
            return new ValueTask<ListToolsResult>(new ListToolsResult { Tools = new List<ModelContextProtocol.Protocol.Tool>() });
        }

        public ValueTask<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?>? toolArguments, CancellationToken ct)
        {
            if (_callToolAsyncImpl != null)
                return _callToolAsyncImpl(toolName, toolArguments, ct);
            return new ValueTask<CallToolResult>(new CallToolResult { Content = [], IsError = false });
        }
    }

    [Fact]
    public async Task SendMessageAsync_NoApiKeyConfigured_ReturnsSafeErrorMessage()
    {
        // Arrange
        var keyStoreMock = new Mock<ILlmApiKeyStore>();
        keyStoreMock
            .Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ChatOrchestrator>>();

        var orchestrator = new ChatOrchestrator(
            keyStoreMock.Object,
            httpClientFactoryMock.Object,
            loggerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IConfiguration>().Object);

        // Act
        var result = await orchestrator.SendMessageAsync(EmptyHistory, "Hello", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Reply);
        Assert.Contains("not configured", result.Reply.ToLowerInvariant());
        Assert.Empty(result.ToolsInvoked);
    }

    [Fact]
    public async Task SendMessageAsync_TextOnlyReply_ReturnsFinalReplyWithoutToolInvocations()
    {
        // Arrange
        var keyStoreMock = new Mock<ILlmApiKeyStore>();
        keyStoreMock.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>())).ReturnsAsync("Anthropic");
        keyStoreMock.Setup(x => x.GetDecryptedApiKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test-key");
        keyStoreMock.Setup(x => x.GetModelAsync(It.IsAny<CancellationToken>())).ReturnsAsync("claude-3-sonnet-20240229");
        keyStoreMock.Setup(x => x.GetBaseUrlAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ChatOrchestrator>>();

        // Mock LLM provider that returns text-only reply
        var llmProviderMock = new Mock<ILlmChatProvider>();
        llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult(TextReply: "Hello, how can I help?", ToolCallId: null, ToolName: null, ToolArgumentsJson: null));

        Func<string, string, string?, ILlmChatProvider> providerFactory = (provider, key, baseUrl) => llmProviderMock.Object;

        var callToolAsyncCalls = new List<(string toolName, IReadOnlyDictionary<string, object?>? args)>();

        // Create test MCP tool client
        var mcpToolClient = new TestMcpToolClient(
            listToolsAsyncImpl: (req, ct) => new ValueTask<ListToolsResult>(new ListToolsResult { Tools = new List<ModelContextProtocol.Protocol.Tool>() }),
            callToolAsyncImpl: (toolName, args, ct) =>
            {
                callToolAsyncCalls.Add((toolName, args));
                return new ValueTask<CallToolResult>(new CallToolResult { Content = [], IsError = false });
            });

        Func<Task<IMcpToolClient>> mcpToolClientFactory = () => Task.FromResult<IMcpToolClient>(mcpToolClient);

        var orchestrator = new ChatOrchestrator(
            keyStoreMock.Object,
            httpClientFactoryMock.Object,
            loggerMock.Object,
            providerFactory,
            mcpToolClientFactory);

        // Act
        var result = await orchestrator.SendMessageAsync(EmptyHistory, "Hello", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello, how can I help?", result.Reply);
        Assert.Empty(result.ToolsInvoked);
        Assert.Empty(callToolAsyncCalls); // CallToolAsync should not have been called
    }

    [Fact]
    public async Task SendMessageAsync_ToolCallRoundTrip_ReturnsTextAfterToolInvocation()
    {
        // Arrange
        var keyStoreMock = new Mock<ILlmApiKeyStore>();
        keyStoreMock.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>())).ReturnsAsync("Anthropic");
        keyStoreMock.Setup(x => x.GetDecryptedApiKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test-key");
        keyStoreMock.Setup(x => x.GetModelAsync(It.IsAny<CancellationToken>())).ReturnsAsync("claude-3-sonnet-20240229");
        keyStoreMock.Setup(x => x.GetBaseUrlAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ChatOrchestrator>>();

        // Mock LLM provider: first call returns tool call, second call returns text
        var llmProviderMock = new Mock<ILlmChatProvider>();
        llmProviderMock
            .SetupSequence(x => x.CompleteAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult(TextReply: null, ToolCallId: "call-123", ToolName: "get_info", ToolArgumentsJson: "{}"))
            .ReturnsAsync(new LlmCompletionResult(TextReply: "Tool says: success", ToolCallId: null, ToolName: null, ToolArgumentsJson: null));

        Func<string, string, string?, ILlmChatProvider> providerFactory = (provider, key, baseUrl) => llmProviderMock.Object;

        // Create tool list for testing
        var toolsList = new List<ModelContextProtocol.Protocol.Tool>
        {
            new ModelContextProtocol.Protocol.Tool
            {
                Name = "get_info",
                Description = "Get info",
                InputSchema = JsonSerializer.Deserialize<JsonElement>(@"{""type"":""object"",""properties"":{},""required"":[]}")
            }
        };

        var callToolAsyncCalls = new List<(string toolName, IReadOnlyDictionary<string, object?>? args)>();

        // Create test MCP tool client
        var mcpToolClient = new TestMcpToolClient(
            listToolsAsyncImpl: (req, ct) => new ValueTask<ListToolsResult>(new ListToolsResult { Tools = toolsList }),
            callToolAsyncImpl: (toolName, args, ct) =>
            {
                callToolAsyncCalls.Add((toolName, args));
                var result = new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "Tool executed" }],
                    IsError = false
                };
                return new ValueTask<CallToolResult>(result);
            });

        Func<Task<IMcpToolClient>> mcpToolClientFactory = () => Task.FromResult<IMcpToolClient>(mcpToolClient);

        var orchestrator = new ChatOrchestrator(
            keyStoreMock.Object,
            httpClientFactoryMock.Object,
            loggerMock.Object,
            providerFactory,
            mcpToolClientFactory);

        // Act
        var result = await orchestrator.SendMessageAsync(EmptyHistory, "Hello", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Tool says: success", result.Reply);
        Assert.Single(result.ToolsInvoked);
        Assert.Contains("get_info", result.ToolsInvoked);
        Assert.Single(callToolAsyncCalls);
        Assert.Equal("get_info", callToolAsyncCalls[0].toolName);
    }

    [Fact]
    public async Task SendMessageAsync_LoopCapExceeded_ReturnsSafeMaxToolCallsMessage()
    {
        // Arrange
        var keyStoreMock = new Mock<ILlmApiKeyStore>();
        keyStoreMock.Setup(x => x.GetProviderAsync(It.IsAny<CancellationToken>())).ReturnsAsync("Anthropic");
        keyStoreMock.Setup(x => x.GetDecryptedApiKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test-key");
        keyStoreMock.Setup(x => x.GetModelAsync(It.IsAny<CancellationToken>())).ReturnsAsync("claude-3-sonnet-20240229");
        keyStoreMock.Setup(x => x.GetBaseUrlAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ChatOrchestrator>>();

        // Mock LLM provider that ALWAYS returns tool call (never text)
        var llmProviderMock = new Mock<ILlmChatProvider>();
        llmProviderMock
            .Setup(x => x.CompleteAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<IReadOnlyList<LlmToolDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult(TextReply: null, ToolCallId: "call-x", ToolName: "tool_x", ToolArgumentsJson: "{}"));

        Func<string, string, string?, ILlmChatProvider> providerFactory = (provider, key, baseUrl) => llmProviderMock.Object;

        // Create tool list
        var toolsList2 = new List<ModelContextProtocol.Protocol.Tool>
        {
            new ModelContextProtocol.Protocol.Tool
            {
                Name = "tool_x",
                Description = "Tool",
                InputSchema = JsonSerializer.Deserialize<JsonElement>(@"{""type"":""object"",""properties"":{},""required"":[]}")
            }
        };

        var callToolAsyncCallCount = 0;

        // Create test MCP tool client
        var mcpToolClient = new TestMcpToolClient(
            listToolsAsyncImpl: (req, ct) => new ValueTask<ListToolsResult>(new ListToolsResult { Tools = toolsList2 }),
            callToolAsyncImpl: (toolName, args, ct) =>
            {
                callToolAsyncCallCount++;
                var result = new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "result" }],
                    IsError = false
                };
                return new ValueTask<CallToolResult>(result);
            });

        Func<Task<IMcpToolClient>> mcpToolClientFactory = () => Task.FromResult<IMcpToolClient>(mcpToolClient);

        var orchestrator = new ChatOrchestrator(
            keyStoreMock.Object,
            httpClientFactoryMock.Object,
            loggerMock.Object,
            providerFactory,
            mcpToolClientFactory);

        // Act
        var result = await orchestrator.SendMessageAsync(EmptyHistory, "Hello", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("maximum number of tool invocations", result.Reply.ToLowerInvariant());
        Assert.Equal(5, result.ToolsInvoked.Count); // MaxToolCalls = 5
        Assert.Equal(5, callToolAsyncCallCount); // CallToolAsync was called 5 times
    }
}
