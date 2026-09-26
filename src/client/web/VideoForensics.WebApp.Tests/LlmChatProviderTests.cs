using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VideoForensics.WebApp.Chat;
using Xunit;

namespace VideoForensics.WebApp.Tests;

/// <summary>Common test utilities for LLM chat providers.</summary>
internal static class LlmChatProviderTestHelpers
{
    public static LlmMessage UserMessage(string content) => new("user", content);
    public static LlmMessage AssistantMessage(string content) => new("assistant", content);
    public static LlmMessage ToolResultMessage(string toolCallId, string result) => new("tool", result, ToolCallId: toolCallId);
}

/// <summary>Tests for AnthropicChatProvider.</summary>
public class AnthropicChatProvider_Tests
{
    private static readonly AnthropicChatOptions TestOptions = new()
    {
        ApiKey = "test-key",
        Model = "claude-3-5-sonnet-20241022"
    };

    private static readonly LlmToolDefinition TestTool = new(
        Name: "test_tool",
        Description: "A test tool",
        JsonSchema: """{"type":"object","properties":{"param":{"type":"string"}},"required":["param"]}"""
    );

    [Fact]
    public async Task CompleteAsync_TextReply_ReturnsTextReplyResult()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(response: new
        {
            content = new[]
            {
                new { type = "text", text = "Hello, world!" }
            }
        });
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Hello") };

        // Act
        var result = await provider.CompleteAsync(messages, Array.Empty<LlmToolDefinition>(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello, world!", result.TextReply);
        Assert.False(result.IsToolCall);
        Assert.Null(result.ToolName);
    }

    [Fact]
    public async Task CompleteAsync_ToolCall_ReturnsToolCallResult()
    {
        // Arrange
        var toolCallId = "call_123";
        var handler = new TestHttpMessageHandler(response: new
        {
            content = new[]
            {
                new
                {
                    type = "tool_use",
                    id = toolCallId,
                    name = "test_tool",
                    input = new { param = "test_value" }
                }
            }
        });
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Call a tool") };

        // Act
        var result = await provider.CompleteAsync(messages, new[] { TestTool }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsToolCall);
        Assert.Equal(toolCallId, result.ToolCallId);
        Assert.Equal("test_tool", result.ToolName);
        Assert.NotNull(result.ToolArgumentsJson);
        Assert.Null(result.TextReply);
    }

    [Fact]
    public async Task CompleteAsync_WithToolResultMessage_IncludesInRequest()
    {
        // Arrange
        var toolCallId = "call_123";
        var handler = new TestHttpMessageHandler(
            requestValidator: req =>
            {
                // Verify the request includes the tool result message
                var body = req.Content?.ReadAsStringAsync().Result ?? "";
                Assert.Contains("tool", body);
                Assert.Contains(toolCallId, body);
                Assert.Contains("tool result content", body);
            },
            response: new
            {
                content = new[] { new { type = "text", text = "Got it" } }
            }
        );
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[]
        {
            LlmChatProviderTestHelpers.UserMessage("Call a tool"),
            new LlmMessage("assistant", null, ToolCallId: toolCallId, ToolName: "test_tool", ToolArgumentsJson: """{"param":"test"}"""),
            LlmChatProviderTestHelpers.ToolResultMessage(toolCallId, "tool result content")
        };

        // Act
        var result = await provider.CompleteAsync(messages, new[] { TestTool }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Got it", result.TextReply);
    }

    [Fact]
    public async Task CompleteAsync_SendsCorrectRequestFormat()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            requestValidator: req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Contains("test-key", req.Headers.GetValues("x-api-key").First());
                Assert.NotNull(req.Content);
            },
            response: new
            {
                content = new[] { new { type = "text", text = "OK" } }
            }
        );
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Test") };

        // Act
        await provider.CompleteAsync(messages, Array.Empty<LlmToolDefinition>(), CancellationToken.None);

        // Assert
        // Handler validation already ran
    }
}

/// <summary>Tests for OpenAiCompatibleChatProvider.</summary>
public class OpenAiCompatibleChatProvider_Tests
{
    private static readonly OpenAiChatOptions TestOptions = new()
    {
        ApiKey = "test-key",
        Model = "gpt-4"
    };

    private static readonly LlmToolDefinition TestTool = new(
        Name: "test_tool",
        Description: "A test tool",
        JsonSchema: """{"type":"object","properties":{"param":{"type":"string"}},"required":["param"]}"""
    );

    [Fact]
    public async Task CompleteAsync_TextReply_ReturnsTextReplyResult()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(response: new
        {
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content = "Hello, world!" }
                }
            }
        });
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Hello") };

        // Act
        var result = await provider.CompleteAsync(messages, Array.Empty<LlmToolDefinition>(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello, world!", result.TextReply);
        Assert.False(result.IsToolCall);
        Assert.Null(result.ToolName);
    }

    [Fact]
    public async Task CompleteAsync_ToolCall_ReturnsToolCallResult()
    {
        // Arrange
        var toolCallId = "call_123";
        var handler = new TestHttpMessageHandler(response: new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id = toolCallId,
                                function = new
                                {
                                    name = "test_tool",
                                    arguments = """{"param":"test_value"}"""
                                }
                            }
                        }
                    }
                }
            }
        });
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Call a tool") };

        // Act
        var result = await provider.CompleteAsync(messages, new[] { TestTool }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsToolCall);
        Assert.Equal(toolCallId, result.ToolCallId);
        Assert.Equal("test_tool", result.ToolName);
        Assert.Equal("""{"param":"test_value"}""", result.ToolArgumentsJson);
        Assert.Null(result.TextReply);
    }

    [Fact]
    public async Task CompleteAsync_WithToolResultMessage_IncludesInRequest()
    {
        // Arrange
        var toolCallId = "call_123";
        var handler = new TestHttpMessageHandler(
            requestValidator: req =>
            {
                // Verify the request includes the tool result message
                var body = req.Content?.ReadAsStringAsync().Result ?? "";
                Assert.Contains("tool", body);
                Assert.Contains(toolCallId, body);
                Assert.Contains("tool result content", body);
            },
            response: new
            {
                choices = new[] { new { message = new { role = "assistant", content = "Got it" } } }
            }
        );
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[]
        {
            LlmChatProviderTestHelpers.UserMessage("Call a tool"),
            new LlmMessage("assistant", null, ToolCallId: toolCallId, ToolName: "test_tool", ToolArgumentsJson: """{"param":"test"}"""),
            LlmChatProviderTestHelpers.ToolResultMessage(toolCallId, "tool result content")
        };

        // Act
        var result = await provider.CompleteAsync(messages, new[] { TestTool }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Got it", result.TextReply);
    }

    [Fact]
    public async Task CompleteAsync_SendsCorrectRequestFormat()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            requestValidator: req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Contains("test-key", req.Headers.GetValues("Authorization").First());
                Assert.NotNull(req.Content);
            },
            response: new
            {
                choices = new[] { new { message = new { role = "assistant", content = "OK" } } }
            }
        );
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Test") };

        // Act
        await provider.CompleteAsync(messages, Array.Empty<LlmToolDefinition>(), CancellationToken.None);

        // Assert
        // Handler validation already ran
    }
}

/// <summary>Mock HTTP message handler for testing.</summary>
internal class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Action<HttpRequestMessage>? _requestValidator;
    private readonly object _response;

    public TestHttpMessageHandler(object response, Action<HttpRequestMessage>? requestValidator = null)
    {
        _response = response;
        _requestValidator = requestValidator;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestValidator?.Invoke(request);

        var json = JsonSerializer.Serialize(_response);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = content
        });
    }
}
