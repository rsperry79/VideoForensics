using System.Net;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteChatServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;
            public HttpRequestMessage? CapturedRequest { get; private set; }
            public HttpContent? CapturedContent { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                CapturedContent = request.Content;
                return await _responseFactory(request);
            }
        }

        private static HttpClient CreateHttpClientWithHandler(FakeHttpMessageHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        }

        [Fact]
        public async Task SendMessageAsync_PostsCorrectJsonToEndpoint_AndReturnsReply()
        {
            var history = new List<ChatTurn>
            {
                new ChatTurn { Role = "user", Content = "Hello" },
                new ChatTurn { Role = "assistant", Content = "Hi there!" }
            };
            string userMessage = "What's the weather?";
            var response = new ChatResponseDto("It's sunny", new List<string> { "weather_tool" });
            string json = JsonSerializer.Serialize(response, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("/api/v1/chat", handler.CapturedRequest.RequestUri?.PathAndQuery);
            Assert.Equal("It's sunny", result.Reply);
            Assert.Single(result.ToolsInvoked);
            Assert.Equal("weather_tool", result.ToolsInvoked[0]);
        }

        [Fact]
        public async Task SendMessageAsync_WithCancellationToken_PassesToHttpClient()
        {
            var cts = new CancellationTokenSource();
            var history = new List<ChatTurn>();
            string userMessage = "Test";
            var response = new ChatResponseDto("Reply", new List<string>());
            string json = JsonSerializer.Serialize(response, JsonOptions);
            CancellationToken? capturedToken = null;

            FakeHttpMessageHandler handler = new(async req =>
            {
                capturedToken = cts.Token;
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, cts.Token);

            _ = Assert.NotNull(capturedToken);
            Assert.Equal(cts.Token, capturedToken.Value);
        }

        [Fact]
        public async Task SendMessageAsync_EmptyHistory_SendsEmptyHistoryList()
        {
            var history = new List<ChatTurn>();
            string userMessage = "Hello";
            var response = new ChatResponseDto("Hi", new List<string>());
            string json = JsonSerializer.Serialize(response, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Hi", result.Reply);
        }

        [Fact]
        public async Task SendMessageAsync_NoToolsInvoked_ReturnsEmptyToolsList()
        {
            var history = new List<ChatTurn>();
            string userMessage = "Test";
            var response = new ChatResponseDto("Reply", new List<string>());
            string json = JsonSerializer.Serialize(response, JsonOptions);

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, CancellationToken.None);

            Assert.Empty(result.ToolsInvoked);
        }

        [Fact]
        public async Task SendMessageAsync_MultipleHistoryEntries_MapsAllCorrectly()
        {
            var history = new List<ChatTurn>
            {
                new ChatTurn { Role = "user", Content = "First message" },
                new ChatTurn { Role = "assistant", Content = "First reply" },
                new ChatTurn { Role = "user", Content = "Second message" }
            };
            string userMessage = "Third message";
            var response = new ChatResponseDto("Third reply", new List<string>());
            string json = JsonSerializer.Serialize(response, JsonOptions);

            ChatRequestDto? capturedRequest = null;
            FakeHttpMessageHandler handler = new(async req =>
            {
                if (req.Content is not null)
                {
                    string content = await req.Content.ReadAsStringAsync();
                    capturedRequest = JsonSerializer.Deserialize<ChatRequestDto>(content, JsonOptions);
                }
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, CancellationToken.None);

            Assert.NotNull(capturedRequest);
            Assert.Equal(3, capturedRequest.History.Count);
            Assert.Equal("user", capturedRequest.History[0].Role);
            Assert.Equal("First message", capturedRequest.History[0].Content);
            Assert.Equal("assistant", capturedRequest.History[1].Role);
            Assert.Equal("First reply", capturedRequest.History[1].Content);
            Assert.Equal("user", capturedRequest.History[2].Role);
            Assert.Equal("Second message", capturedRequest.History[2].Content);
            Assert.Equal(userMessage, capturedRequest.Message);
        }

        [Fact]
        public async Task SendMessageAsync_NullResponse_ReturnsDefaultResult()
        {
            var history = new List<ChatTurn>();
            string userMessage = "Test";

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            ChatTurnResult result = await service.SendMessageAsync(history, userMessage, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Reply);
            Assert.Empty(result.ToolsInvoked);
        }

        [Fact]
        public async Task SendMessageAsync_HttpError_ThrowsException()
        {
            var history = new List<ChatTurn>();
            string userMessage = "Test";

            FakeHttpMessageHandler handler = new(async req =>
            {
                await Task.Yield();
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error", System.Text.Encoding.UTF8, "application/json")
                };
            });

            HttpClient httpClient = CreateHttpClientWithHandler(handler);
            var service = new RemoteChatService(httpClient);

            HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.SendMessageAsync(history, userMessage, CancellationToken.None));

            Assert.Contains("500", ex.Message);
        }
    }
}
