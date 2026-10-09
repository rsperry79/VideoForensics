using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Exercises the real routing and authorization of the chat endpoints (including the SSE stream)
    /// through an in-memory TestServer.
    /// </summary>
    public class ChatEndpointsPipelineTests
    {
        private const string TestScheme = "ChatTest";
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (Request.Headers.ContainsKey("X-Test-Anonymous"))
                    return Task.FromResult(AuthenticateResult.NoResult());

                string role = Request.Headers.TryGetValue("X-Test-Role", out var r) ? r.ToString() : "ReadOnly";
                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, role),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, "Local"),
                    new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
                ], TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        private sealed class FakeChatService : IChatService
        {
            public required Func<IReadOnlyList<ChatTurn>, string, IAsyncEnumerable<ChatStreamEvent>> Stream { get; init; }
            public Mock<IChatService> Inner { get; } = new();

            public Task<ChatTurnResult> SendMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
                => Inner.Object.SendMessageAsync(history, message, ct);

            public IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
                => Stream(history, message);
        }

        private static async IAsyncEnumerable<ChatStreamEvent> Events(params ChatStreamEvent[] events)
        {
            foreach (var e in events)
            {
                await Task.Yield();
                yield return e;
            }
        }

        private sealed class TestHost : IAsyncDisposable
        {
            public required WebApplication App { get; init; }
            public required HttpClient Client { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<TestHost> StartAsync(IChatService chat)
        {
            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            _ = builder.Services.AddSingleton(chat);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });

            WebApplication app = builder.Build();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapChatEndpoints();
            await app.StartAsync();

            return new TestHost { App = app, Client = app.GetTestClient() };
        }

        private static HttpRequestMessage Post(string url, object body, string? role = "ReadOnly", bool anonymous = false)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            if (anonymous) req.Headers.Add("X-Test-Anonymous", "1");
            else if (role != null) req.Headers.Add("X-Test-Role", role);
            return req;
        }

        private static ChatRequestDto Req(string message) => new(Array.Empty<ChatMessageDto>(), message);

        [Fact]
        public async Task PostChat_Anonymous_Returns401()
        {
            var chat = new FakeChatService { Stream = (_, _) => Events() };
            await using var host = await StartAsync(chat);

            var resp = await host.Client.SendAsync(Post("/api/v1/chat", Req("hi"), anonymous: true));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task PostChatStream_ReadOnly_EmitsDeltaToolDeltaDoneInOrder()
        {
            var result = new ChatTurnResult { Reply = "Hello", ToolsInvoked = ["some_tool"] };
            var chat = new FakeChatService
            {
                Stream = (_, _) => Events(
                    new ChatTextDelta("Hel"),
                    new ChatToolInvoked("some_tool"),
                    new ChatTextDelta("lo"),
                    new ChatTurnCompleted(result))
            };
            await using var host = await StartAsync(chat);

            var resp = await host.Client.SendAsync(Post("/api/v1/chat/stream", Req("hi")));
            string body = await resp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);
            var names = body.Split('\n').Where(l => l.StartsWith("event:")).Select(l => l["event:".Length..].Trim()).ToList();
            Assert.Equal(new[] { "delta", "tool", "delta", "done" }, names);
        }

        [Theory]
        [InlineData("/api/v1/chat")]
        [InlineData("/api/v1/chat/stream")]
        public async Task PostChat_EmptyMessage_Returns400(string url)
        {
            var chat = new FakeChatService { Stream = (_, _) => Events() };
            await using var host = await StartAsync(chat);

            var resp = await host.Client.SendAsync(Post(url, Req("   ")));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task PostChat_ReadOnly_ReturnsReplyFromService()
        {
            var chat = new FakeChatService { Stream = (_, _) => Events() };
            chat.Inner.Setup(c => c.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "hi", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatTurnResult { Reply = "pong", ToolsInvoked = ["t1"] });
            await using var host = await StartAsync(chat);

            var resp = await host.Client.SendAsync(Post("/api/v1/chat", Req("hi")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<ChatResponseDto>(WebJson);
            Assert.NotNull(dto);
            Assert.Equal("pong", dto.Reply);
            Assert.Equal(new[] { "t1" }, dto.ToolsInvoked);
        }

        [Fact]
        public void ToSseFrame_TextDelta_ReturnsDeltaWithText()
        {
            var (name, data) = ChatEndpoints.ToSseFrame(new ChatTextDelta("abc"));

            Assert.Equal("delta", name);
            using var doc = JsonDocument.Parse(data);
            Assert.Equal("abc", doc.RootElement.GetProperty("text").GetString());
        }

        [Fact]
        public void ToSseFrame_ToolInvoked_ReturnsToolWithName()
        {
            var (name, data) = ChatEndpoints.ToSseFrame(new ChatToolInvoked("my_tool"));

            Assert.Equal("tool", name);
            using var doc = JsonDocument.Parse(data);
            Assert.Equal("my_tool", doc.RootElement.GetProperty("name").GetString());
        }

        [Fact]
        public void ToSseFrame_TurnCompleted_ReturnsDoneWithResultDto()
        {
            var (name, data) = ChatEndpoints.ToSseFrame(new ChatTurnCompleted(new ChatTurnResult { Reply = "r", ToolsInvoked = ["a", "b"] }));

            Assert.Equal("done", name);
            using var doc = JsonDocument.Parse(data);
            Assert.Equal("r", doc.RootElement.GetProperty("reply").GetString());
            Assert.Equal(2, doc.RootElement.GetProperty("toolsInvoked").GetArrayLength());
        }
    }
}
