using System.Net;
using System.Net.Http.Headers;
using System.Text;

using VideoForensics.Utils.DbRepair.Contracts;

using Xunit;

namespace VideoForensics.Utils.DbRepair.Tests;

public class ServerMaintenanceClient_Tests
{
    private static readonly Uri Server = new("http://localhost:5162/");
    private static readonly TimeSpan RenewInterval = TimeSpan.FromMinutes(10);
    private static readonly Guid HoldToken = Guid.Parse("6f1c2b7e-3a4d-4e5f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public async Task StartAsync_ServerUnreachableAndDeclined_ReturnsNullWithoutHold()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));
        var console = new FakeConsole(interactive: true, answer: false);
        using var http = new HttpClient(handler);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await new ServerMaintenanceClient(http, console).StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.Equal(["GET /healthz"], handler.Requests);
        Assert.Single(console.Questions);
    }

    [Fact]
    public async Task StartAsync_ServerUnreachableNonInteractive_ReturnsNullWithoutPrompt()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));
        var console = new FakeConsole(interactive: false, answer: true);
        using var http = new HttpClient(handler);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await new ServerMaintenanceClient(http, console).StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.Empty(console.Questions);
        Assert.Equal(["GET /healthz"], handler.Requests);
    }

    [Fact]
    public async Task StartAsync_ServerUnreachableAndAccepted_ReturnsSessionWithoutHold()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));
        var console = new FakeConsole(interactive: true, answer: true);
        using var http = new HttpClient(handler);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await new ServerMaintenanceClient(http, console).StartAsync(Server, CancellationToken.None);

        Assert.NotNull(session);
        Assert.False(session.Held);
        Assert.Equal(["GET /healthz"], handler.Requests);
    }

    [Fact]
    public async Task StartAsync_Approved_HoldsWithTokenAndEndsWithThatToken()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        Assert.NotNull(session);
        Assert.True(session.Held);
        await session.DisposeAsync();

        int endIndex = handler.Requests.FindIndex(r => r == "POST /api/v1/database/maintenance/end");
        Assert.True(endIndex >= 0);
        Assert.Contains(HoldToken.ToString(), handler.Bodies[endIndex]);
    }

    [Fact]
    public async Task StartAsync_PollingUsesServerIntervalSeconds()
    {
        var handler = new FakeHandler(ServerRoutes(intervalSeconds: 5, pollResponses: ["pending", "approved"]));
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        await session!.DisposeAsync();

        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], delay.PollDelays);
    }

    [Fact]
    public async Task StartAsync_IntervalBelowTwoSeconds_PollsEveryTwoSeconds()
    {
        var handler = new FakeHandler(ServerRoutes(intervalSeconds: 1, pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceSession_Dispose(await client.StartAsync(Server, CancellationToken.None));

        Assert.Equal([TimeSpan.FromSeconds(2)], delay.PollDelays);
    }

    [Fact]
    public async Task StartAsync_PollReturnsTooManyRequests_RetriesAndStillHolds()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: [HttpStatusCode.TooManyRequests, "approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.NotNull(session);
        Assert.True(session.Held);
        Assert.Equal(2, handler.Requests.Count(r => r.EndsWith("/poll")));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_PollReturnsServerError_RetriesAndStillHolds()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: [HttpStatusCode.InternalServerError, "approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.NotNull(session);
        Assert.True(session.Held);
        Assert.Equal(2, handler.Requests.Count(r => r.EndsWith("/poll")));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_PollReturnsOtherClientError_FailsWithoutBegin()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: [HttpStatusCode.BadRequest]));
        using var http = new HttpClient(handler);
        var console = new FakeConsole(interactive: true, answer: false);
        var client = new ServerMaintenanceClient(http, console, NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.DoesNotContain("POST /api/v1/database/maintenance/begin", handler.Requests);
    }

    [Fact]
    public async Task StartAsync_BeginResponseHasNoToken_HoldsNothingAndSendsNoEnd()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"], beginBody: "{}"));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: true), NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        Assert.NotNull(session);
        Assert.False(session.Held);
        await session.DisposeAsync();

        Assert.DoesNotContain("POST /api/v1/database/maintenance/end", handler.Requests);
    }

    [Fact]
    public async Task StartAsync_BeginLostAfterSend_NoEndIsSentBecauseTokenIsUnknown()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/maintenance/begin")
            ? throw new HttpRequestException("response lost")
            : ServerRoutes(pollResponses: ["approved"])(request));
        using var http = new HttpClient(handler);
        var console = new FakeConsole(interactive: true, answer: false);
        var client = new ServerMaintenanceClient(http, console, NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.DoesNotContain("POST /api/v1/database/maintenance/end", handler.Requests);
    }

    [Fact]
    public async Task StartAsync_Held_RenewsWithTokenWhileRepairRuns()
    {
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew"))
            {
                renewed.TrySetResult();
            }

            return ServerRoutes(pollResponses: ["approved"])(request);
        });
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        Assert.NotNull(session);

        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.DisposeAsync();

        Assert.Contains(RenewInterval, delay.RenewDelays);
        int renewIndex = handler.Requests.FindIndex(r => r == "POST /api/v1/database/maintenance/renew");
        Assert.True(renewIndex >= 0);
        Assert.Contains(HoldToken.ToString(), handler.Bodies[renewIndex]);
    }

    [Fact]
    public async Task StartAsync_ServerUrlHasNoHttpsOrLoopback_IsRejectedByValidator()
    {
        bool ok = ServerMaintenanceClient.TryValidateServer("http://192.168.1.20:5162/", out Uri? server, out string? error);

        Assert.False(ok);
        Assert.Null(server);
        Assert.Contains("https", error);
    }

    [Theory]
    [InlineData("http://localhost:5162/")]
    [InlineData("http://127.0.0.1:5162/")]
    [InlineData("http://[::1]:5162/")]
    [InlineData("https://example.com/")]
    [InlineData("https://192.168.1.20:5162/")]
    public void TryValidateServer_AllowedUrls_Accepted(string value)
    {
        bool ok = ServerMaintenanceClient.TryValidateServer(value, out Uri? server, out string? error);

        Assert.True(ok, error);
        Assert.NotNull(server);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("http://192.168.1.20:5162/")]
    [InlineData("ftp://localhost/")]
    [InlineData("localhost:5162")]
    [InlineData("not a url")]
    public void TryValidateServer_RejectedUrls_ReturnsFalseWithMessage(string value)
    {
        bool ok = ServerMaintenanceClient.TryValidateServer(value, out Uri? server, out string? error);

        Assert.False(ok);
        Assert.Null(server);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    // Starts a session with a session-less dispose helper so the poll test can dispose a nullable result.
    private static void ServerMaintenanceSession_Dispose(ServerMaintenanceClient.ServerMaintenanceSession? session) =>
        session?.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // Answers the server routes the client calls. Poll responses are consumed in order; the last one repeats.
    private static Func<HttpRequestMessage, HttpResponseMessage> ServerRoutes(
        int intervalSeconds = 5,
        object[]? pollResponses = null,
        string? beginBody = null)
    {
        Queue<object> polls = new(pollResponses ?? ["approved"]);
        return request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/healthz")
            {
                return Json(HttpStatusCode.OK, "{}");
            }

            if (path == "/api/v1/pairing/device-code")
            {
                return Json(HttpStatusCode.OK, $"{{\"deviceCode\":\"dc-1\",\"userCode\":\"ABCD-1234\",\"intervalSeconds\":{intervalSeconds}}}");
            }

            if (path.EndsWith("/poll"))
            {
                object next = polls.Count > 1 ? polls.Dequeue() : polls.Peek();
                return next switch
                {
                    HttpStatusCode status => new HttpResponseMessage(status),
                    "approved" => Json(HttpStatusCode.OK, "{\"status\":\"approved\",\"apiKey\":\"key-1\"}"),
                    _ => Json(HttpStatusCode.OK, "{\"status\":\"pending\"}"),
                };
            }

            if (path == "/api/v1/database/maintenance/begin")
            {
                return Json(HttpStatusCode.OK, beginBody ?? $"{{\"holdToken\":\"{HoldToken}\"}}");
            }

            if (path == "/api/v1/database/maintenance/renew")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path == "/api/v1/database/maintenance/end")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static FakeDelay NewDelay() => new();

    private sealed class FakeDelay
    {
        public List<TimeSpan> PollDelays { get; } = [];

        public List<TimeSpan> RenewDelays { get; } = [];

        private int _renewWaits;

        // Poll waits complete at once. The first renew wait completes so one renew happens; later renew waits block until cancelled.
        public Task Delay(TimeSpan delay, CancellationToken ct)
        {
            if (delay == RenewInterval)
            {
                RenewDelays.Add(delay);
                return Interlocked.Increment(ref _renewWaits) == 1 ? Task.CompletedTask : Task.Delay(Timeout.Infinite, ct);
            }

            PollDelays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            return respond(request);
        }
    }

    private sealed class FakeConsole(bool interactive, bool answer) : IMaintenanceConsole
    {
        public List<string> Questions { get; } = [];

        public bool IsInteractive => interactive;

        public void Write(string message)
        {
        }

        public Task<bool> ConfirmAsync(string question, CancellationToken ct)
        {
            Questions.Add(question);
            return Task.FromResult(answer);
        }

        public bool OpenBrowser(Uri url) => false;
    }
}
