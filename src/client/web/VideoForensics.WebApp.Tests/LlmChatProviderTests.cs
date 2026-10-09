using System.Net;
using System.Text;
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

    /// <summary>Builds a server-sent-events body from (event, data) frames. Pass null as event for data-only frames.</summary>
    public static string Sse(params (string? Event, string Data)[] frames)
    {
        var sb = new StringBuilder();
        foreach (var (evt, data) in frames)
        {
            if (evt != null) sb.Append("event: ").Append(evt).Append('\n');
            sb.Append("data: ").Append(data).Append("\n\n");
        }
        return sb.ToString();
    }

    /// <summary>Drains a provider stream, returning text deltas in order and the single step completion.</summary>
    public static async Task<(List<string> Deltas, LlmCompletionResult Completion)> CollectAsync(IAsyncEnumerable<LlmStreamEvent> events)
    {
        var deltas = new List<string>();
        LlmCompletionResult? completion = null;
        await foreach (LlmStreamEvent evt in events)
        {
            switch (evt)
            {
                case LlmTextDelta delta:
                    deltas.Add(delta.Text);
                    break;
                case LlmStepCompleted step:
                    Assert.Null(completion); // exactly one completion per step
                    completion = step.Result;
                    break;
            }
        }
        Assert.NotNull(completion);
        return (deltas, completion);
    }
}

/// <summary>Tests for AnthropicChatProvider streaming.</summary>
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

    private const string TextBlockStart = """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""";
    private const string BlockStop = """{"type":"content_block_stop","index":0}""";
    private const string MessageStop = """{"type":"message_stop"}""";

    private static string TextDelta(string text) =>
        JsonSerializer.Serialize(new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text } });

    [Fact]
    public async Task StreamCompleteAsync_TextReply_YieldsDeltasThenCompletion()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            ("message_start", """{"type":"message_start"}"""),
            ("content_block_start", TextBlockStart),
            ("content_block_delta", TextDelta("Hello, ")),
            ("content_block_delta", TextDelta("world!")),
            ("content_block_stop", BlockStop),
            ("message_stop", MessageStop));
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Hello") };

        // Act
        var (deltas, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(messages, Array.Empty<LlmToolDefinition>(), CancellationToken.None));

        // Assert
        Assert.Equal(new[] { "Hello, ", "world!" }, deltas);
        Assert.Equal("Hello, world!", result.TextReply);
        Assert.False(result.IsToolCall);
    }

    [Fact]
    public async Task StreamCompleteAsync_ToolCall_AccumulatesPartialJsonIntoArguments()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            ("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"test_tool","input":{}}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"param\":"}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"\"test_value\"}"}}"""),
            ("content_block_stop", BlockStop),
            ("message_stop", MessageStop));
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));
        var messages = new[] { LlmChatProviderTestHelpers.UserMessage("Call a tool") };

        // Act
        var (deltas, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(messages, new[] { TestTool }, CancellationToken.None));

        // Assert
        Assert.Empty(deltas);
        Assert.True(result.IsToolCall);
        Assert.Equal("toolu_1", result.ToolCallId);
        Assert.Equal("test_tool", result.ToolName);
        Assert.Equal("""{"param":"test_value"}""", result.ToolArgumentsJson);
        Assert.Null(result.TextReply);
    }

    [Fact]
    public async Task StreamCompleteAsync_TextBeforeToolCall_StillReportsToolCall()
    {
        // Arrange: a step that says "Let me check" and then calls a tool. Text is streamed, the tool call is the result.
        string sse = LlmChatProviderTestHelpers.Sse(
            ("content_block_start", TextBlockStart),
            ("content_block_delta", TextDelta("Let me check.")),
            ("content_block_stop", BlockStop),
            ("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_2","name":"test_tool","input":{}}}"""),
            ("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{}"}}"""),
            ("content_block_stop", BlockStop),
            ("message_stop", MessageStop));
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));

        // Act
        var (deltas, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("x") }, new[] { TestTool }, CancellationToken.None));

        // Assert
        Assert.Equal(new[] { "Let me check." }, deltas);
        Assert.True(result.IsToolCall);
        Assert.Equal("toolu_2", result.ToolCallId);
    }

    [Fact]
    public async Task StreamCompleteAsync_WithToolResultMessage_IncludesInRequest()
    {
        // Arrange
        var toolCallId = "call_123";
        string sse = LlmChatProviderTestHelpers.Sse(
            ("content_block_start", TextBlockStart),
            ("content_block_delta", TextDelta("Got it")),
            ("content_block_stop", BlockStop),
            ("message_stop", MessageStop));
        var handler = new TestSseHttpMessageHandler(sse, requestValidator: req =>
        {
            var body = req.Content?.ReadAsStringAsync().Result ?? "";
            Assert.Contains("tool_result", body);
            Assert.Contains(toolCallId, body);
            Assert.Contains("tool result content", body);
        });
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[]
        {
            LlmChatProviderTestHelpers.UserMessage("Call a tool"),
            new LlmMessage("assistant", null, ToolCallId: toolCallId, ToolName: "test_tool", ToolArgumentsJson: """{"param":"test"}"""),
            LlmChatProviderTestHelpers.ToolResultMessage(toolCallId, "tool result content")
        };

        // Act
        var (_, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(messages, new[] { TestTool }, CancellationToken.None));

        // Assert
        Assert.Equal("Got it", result.TextReply);
    }

    [Fact]
    public async Task StreamCompleteAsync_SendsStreamingRequestWithAuthHeaders()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            ("content_block_start", TextBlockStart),
            ("content_block_delta", TextDelta("OK")),
            ("content_block_stop", BlockStop),
            ("message_stop", MessageStop));
        var handler = new TestSseHttpMessageHandler(sse, requestValidator: req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("test-key", req.Headers.GetValues("x-api-key").First());
            var body = req.Content?.ReadAsStringAsync().Result ?? "";
            Assert.Contains("\"stream\":true", body);
        });
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));

        // Act
        var (_, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Test") }, Array.Empty<LlmToolDefinition>(), CancellationToken.None));

        // Assert
        Assert.Equal("OK", result.TextReply);
    }

    [Fact]
    public async Task StreamCompleteAsync_NonSuccessStatus_ThrowsHttpRequestExceptionWithResponseBody()
    {
        // Arrange
        const string errorBody = """{"type":"error","error":{"type":"invalid_request_error","message":"model: test-bad-model not found"}}""";
        var handler = new TestSseHttpMessageHandler(errorBody, statusCode: HttpStatusCode.BadRequest);
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(handler));

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            LlmChatProviderTestHelpers.CollectAsync(
                provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Test") }, Array.Empty<LlmToolDefinition>(), CancellationToken.None)));

        // Assert
        Assert.Contains("400", ex.Message);
        Assert.Contains("test-bad-model not found", ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task StreamCompleteAsync_ErrorEvent_Throws()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            ("error", """{"type":"error","error":{"type":"overloaded_error","message":"busy"}}"""));
        var provider = new AnthropicChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LlmChatProviderTestHelpers.CollectAsync(
                provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Test") }, Array.Empty<LlmToolDefinition>(), CancellationToken.None)));
    }
}

/// <summary>Tests for OpenAiCompatibleChatProvider streaming.</summary>
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
    public async Task StreamCompleteAsync_TextReply_YieldsDeltasThenCompletion()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            (null, """{"choices":[{"index":0,"delta":{"role":"assistant","content":"Hi"}}]}"""),
            (null, """{"choices":[{"index":0,"delta":{"content":" there"}}]}"""),
            (null, """{"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}"""),
            (null, "[DONE]"));
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));

        // Act
        var (deltas, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Hello") }, Array.Empty<LlmToolDefinition>(), CancellationToken.None));

        // Assert
        Assert.Equal(new[] { "Hi", " there" }, deltas);
        Assert.Equal("Hi there", result.TextReply);
        Assert.False(result.IsToolCall);
    }

    [Fact]
    public async Task StreamCompleteAsync_ToolCall_AccumulatesArgumentFragments()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            (null, """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"test_tool","arguments":""}}]}}]}"""),
            (null, """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"param\":"}}]}}]}"""),
            (null, """{"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"x\"}"}}]}}]}"""),
            (null, """{"choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}"""),
            (null, "[DONE]"));
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(new TestSseHttpMessageHandler(sse)));

        // Act
        var (deltas, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Call") }, new[] { TestTool }, CancellationToken.None));

        // Assert
        Assert.Empty(deltas);
        Assert.True(result.IsToolCall);
        Assert.Equal("call_1", result.ToolCallId);
        Assert.Equal("test_tool", result.ToolName);
        Assert.Equal("""{"param":"x"}""", result.ToolArgumentsJson);
    }

    [Fact]
    public async Task StreamCompleteAsync_WithToolResultMessage_IncludesInRequest()
    {
        // Arrange
        var toolCallId = "call_123";
        string sse = LlmChatProviderTestHelpers.Sse(
            (null, """{"choices":[{"index":0,"delta":{"content":"Got it"}}]}"""),
            (null, "[DONE]"));
        var handler = new TestSseHttpMessageHandler(sse, requestValidator: req =>
        {
            var body = req.Content?.ReadAsStringAsync().Result ?? "";
            Assert.Contains("tool_call_id", body);
            Assert.Contains(toolCallId, body);
            Assert.Contains("tool result content", body);
        });
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));
        var messages = new[]
        {
            LlmChatProviderTestHelpers.UserMessage("Call a tool"),
            new LlmMessage("assistant", null, ToolCallId: toolCallId, ToolName: "test_tool", ToolArgumentsJson: """{"param":"test"}"""),
            LlmChatProviderTestHelpers.ToolResultMessage(toolCallId, "tool result content")
        };

        // Act
        var (_, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(messages, new[] { TestTool }, CancellationToken.None));

        // Assert
        Assert.Equal("Got it", result.TextReply);
    }

    [Fact]
    public async Task StreamCompleteAsync_SendsStreamingRequestWithBearerAuth()
    {
        // Arrange
        string sse = LlmChatProviderTestHelpers.Sse(
            (null, """{"choices":[{"index":0,"delta":{"content":"OK"}}]}"""),
            (null, "[DONE]"));
        var handler = new TestSseHttpMessageHandler(sse, requestValidator: req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("Bearer test-key", req.Headers.Authorization?.ToString());
            var body = req.Content?.ReadAsStringAsync().Result ?? "";
            Assert.Contains("\"stream\":true", body);
        });
        var provider = new OpenAiCompatibleChatProvider(TestOptions, new HttpClient(handler));

        // Act
        var (_, result) = await LlmChatProviderTestHelpers.CollectAsync(
            provider.StreamCompleteAsync(new[] { LlmChatProviderTestHelpers.UserMessage("Test") }, Array.Empty<LlmToolDefinition>(), CancellationToken.None));

        // Assert
        Assert.Equal("OK", result.TextReply);
    }
}

/// <summary>Mock HTTP message handler that returns a fixed server-sent-events body.</summary>
internal class TestSseHttpMessageHandler : HttpMessageHandler
{
    private readonly string _sseBody;
    private readonly Action<HttpRequestMessage>? _requestValidator;

    private readonly HttpStatusCode _statusCode;

    public TestSseHttpMessageHandler(string sseBody, Action<HttpRequestMessage>? requestValidator = null, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _statusCode = statusCode;
        _sseBody = sseBody;
        _requestValidator = requestValidator;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestValidator?.Invoke(request);

        var content = new StringContent(_sseBody, Encoding.UTF8, "text/event-stream");
        return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = content });
    }
}
