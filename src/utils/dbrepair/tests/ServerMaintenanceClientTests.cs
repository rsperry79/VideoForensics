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
        _ = Assert.Single(console.Questions);
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
    public async Task StartAsync_BeginRedirects_FailsWithRedirectMessageAndDoesNotFollow()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/maintenance/begin")
            ? Redirect("https://elsewhere.example/redirected/begin")
            : ServerRoutes(pollResponses: ["approved"])(request));
        using var http = new HttpClient(handler);
        var console = new FakeConsole(interactive: true, answer: false);
        var client = new ServerMaintenanceClient(http, console, NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.Equal(1, handler.Requests.Count(r => r == "POST /api/v1/database/maintenance/begin"));
        Assert.DoesNotContain(handler.Requests, r => r.Contains("redirected"));
        Assert.Contains(console.Messages, m => m.Contains("redirect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_PollReturnsTooManyRequestsWithRetryAfter_WaitsRetryAfterSeconds()
    {
        bool throttled = false;
        Func<HttpRequestMessage, HttpResponseMessage> routes = ServerRoutes(pollResponses: ["approved"]);
        var handler = new FakeHandler(request =>
        {
            if (!throttled && request.RequestUri!.AbsolutePath.EndsWith("/poll"))
            {
                throttled = true;
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
                return limited;
            }

            return routes(request);
        });
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        await session!.DisposeAsync();

        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)], delay.PollDelays);
    }

    [Fact]
    public async Task StartAsync_RetryAfterBeyondPairingDeadline_WaitIsBoundedByDeadline()
    {
        bool throttled = false;
        Func<HttpRequestMessage, HttpResponseMessage> routes = ServerRoutes(pollResponses: ["approved"]);
        var handler = new FakeHandler(request =>
        {
            if (!throttled && request.RequestUri!.AbsolutePath.EndsWith("/poll"))
            {
                throttled = true;
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));
                return limited;
            }

            return routes(request);
        });
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        await session!.DisposeAsync();

        Assert.Equal(2, delay.PollDelays.Count);
        Assert.True(delay.PollDelays[1] <= TimeSpan.FromMinutes(10), $"wait was {delay.PollDelays[1]}");
        Assert.True(delay.PollDelays[1] >= TimeSpan.FromMinutes(9), $"wait was {delay.PollDelays[1]}");
    }

    [Fact]
    public async Task StartAsync_Held_RenewsWithTokenWhileRepairRuns()
    {
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew"))
            {
                _ = renewed.TrySetResult();
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

    [Fact]
    public async Task StartAsync_PollRetryAfterZero_WaitsAtLeastTwoSeconds()
    {
        bool throttled = false;
        Func<HttpRequestMessage, HttpResponseMessage> routes = ServerRoutes(intervalSeconds: 1, pollResponses: ["approved"]);
        var handler = new FakeHandler(request =>
        {
            if (!throttled && request.RequestUri!.AbsolutePath.EndsWith("/poll"))
            {
                throttled = true;
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }

            return routes(request);
        });
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay);

        ServerMaintenanceSession_Dispose(await client.StartAsync(Server, CancellationToken.None));

        Assert.Equal(2, delay.PollDelays.Count);
        Assert.True(delay.PollDelays[1] >= TimeSpan.FromSeconds(2), $"wait was {delay.PollDelays[1]}");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task StartAsync_RenewStopsHold_CancelsRepairTokenAndReportsHoldLost(HttpStatusCode renewStatus)
    {
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<HttpRequestMessage, HttpResponseMessage> routes = ServerRoutes(pollResponses: ["approved"]);
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew"))
            {
                _ = renewed.TrySetResult();
                return new HttpResponseMessage(renewStatus);
            }

            return routes(request);
        });
        using var http = new HttpClient(handler);
        var console = new FakeConsole(interactive: true, answer: false);
        var client = new ServerMaintenanceClient(http, console, NewDelay().Delay);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);
        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.DisposeAsync();

        Assert.True(repairStop.IsCancellationRequested);
        Assert.True(session.HoldLost);
        _ = Assert.Single(handler.Requests, r => r == "POST /api/v1/database/maintenance/renew");
        Assert.DoesNotContain("POST /api/v1/database/maintenance/end", handler.Requests);
        Assert.DoesNotContain(console.Messages, m => m.Contains("will retry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.Messages, m => m.Contains("revoke this device on the admin page", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(25, true)]
    public async Task StartAsync_RenewFailsWithServerError_HoldLostOnlyWhenNoRenewSucceededFor25Minutes(int minutesSinceLastRenew, bool expectLost)
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        Func<HttpRequestMessage, HttpResponseMessage> routes = ServerRoutes(pollResponses: ["approved"]);
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew"))
            {
                clock.Advance(TimeSpan.FromMinutes(minutesSinceLastRenew));
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return routes(request);
        });
        using var http = new HttpClient(handler);
        FakeDelay delay = NewDelay();
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), delay.Delay, clock);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);
        // Why: waits until the renew failure was handled (hold lost, or the loop began its next wait), so the result is not raced.
        await WaitUntilAsync(() => session.HoldLost || delay.RenewDelays.Count >= 2);
        await session.DisposeAsync();

        Assert.Equal(expectLost, session.HoldLost);
        Assert.Equal(expectLost, repairStop.IsCancellationRequested);
    }

    [Fact]
    public async Task StartAsync_BeginReturnsServiceUnavailable_ReportsDrainWithoutPrompt()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/maintenance/begin")
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : ServerRoutes(pollResponses: ["approved"])(request));
        using var http = new HttpClient(handler);
        var console = new FakeConsole(interactive: true, answer: true);
        var client = new ServerMaintenanceClient(http, console, NewDelay().Delay);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);

        Assert.Null(session);
        Assert.Empty(console.Questions);
        Assert.Contains(console.Messages, m => m.Contains("did not finish its work in time; try again later", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmHoldAsync_RenewRefused_ReturnsFalseAndReportsHoldLost()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : ServerRoutes(pollResponses: ["approved"])(request));
        using var http = new HttpClient(handler);
        // Why: the background renew never fires here, so the only renew is the one ConfirmHoldAsync makes.
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), (wait, ct) => wait == RenewInterval ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);

        bool confirmed = await session.ConfirmHoldAsync(CancellationToken.None);

        Assert.False(confirmed);
        Assert.True(session.HoldLost);
        await session.DisposeAsync();
        _ = Assert.Single(handler.Requests, r => r == "POST /api/v1/database/maintenance/renew");
        Assert.DoesNotContain("POST /api/v1/database/maintenance/end", handler.Requests);
    }

    [Fact]
    public async Task ConfirmHoldAsync_RenewAccepted_ReturnsTrueAndKeepsHold()
    {
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), (wait, ct) => wait == RenewInterval ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask);

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None);
        Assert.NotNull(session);

        bool confirmed = await session.ConfirmHoldAsync(CancellationToken.None);

        Assert.True(confirmed);
        Assert.False(session.HoldLost);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task EnsureHoldFreshAsync_LastRenewOver25MinutesAgo_ReturnsFalseAndMarksHoldLost()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NoBackgroundRenew, clock);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);
        clock.Advance(TimeSpan.FromMinutes(26));

        bool fresh = await session.EnsureHoldFreshAsync(CancellationToken.None);

        Assert.False(fresh);
        Assert.True(session.HoldLost);
        Assert.True(repairStop.IsCancellationRequested);
        Assert.DoesNotContain("POST /api/v1/database/maintenance/renew", handler.Requests);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task EnsureHoldFreshAsync_LastRenewUnder25MinutesAgo_ReturnsTrueAndKeepsHold()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NoBackgroundRenew, clock);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);
        clock.Advance(TimeSpan.FromMinutes(24));

        bool fresh = await session.EnsureHoldFreshAsync(CancellationToken.None);

        Assert.True(fresh);
        Assert.False(session.HoldLost);
        Assert.False(repairStop.IsCancellationRequested);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task ConfirmHoldAsync_HoldAlreadyLost_ReturnsFalseWithoutCallingServer()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var handler = new FakeHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NoBackgroundRenew, clock);
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);
        clock.Advance(TimeSpan.FromMinutes(26));
        Assert.False(await session.EnsureHoldFreshAsync(CancellationToken.None));
        int renewsBefore = handler.Requests.Count(r => r.EndsWith("/maintenance/renew", StringComparison.Ordinal));

        bool confirmed = await session.ConfirmHoldAsync(CancellationToken.None);

        Assert.False(confirmed);
        Assert.Equal(renewsBefore, handler.Requests.Count(r => r.EndsWith("/maintenance/renew", StringComparison.Ordinal)));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task ConfirmHoldAsync_RenewNeverAnswers_TimesOutAndReturnsFalseWithoutLosingHold()
    {
        var handler = new HangingRenewHandler(ServerRoutes(pollResponses: ["approved"]));
        using var http = new HttpClient(handler);
        var client = new ServerMaintenanceClient(http, new FakeConsole(interactive: true, answer: false), NoBackgroundRenew, renewTimeout: TimeSpan.FromMilliseconds(50));
        using var repairStop = new CancellationTokenSource();

        ServerMaintenanceClient.ServerMaintenanceSession? session = await client.StartAsync(Server, CancellationToken.None, repairStop);
        Assert.NotNull(session);

        bool confirmed = await session.ConfirmHoldAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(confirmed);
        Assert.False(session.HoldLost);
        Assert.False(repairStop.IsCancellationRequested);
        await session.DisposeAsync();
    }

    private static Task NoBackgroundRenew(TimeSpan wait, CancellationToken ct)
    {
        return wait == RenewInterval ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask;
    }

    // Answers every route, but a renew never completes, like a server that stopped responding.
    private sealed class HangingRenewHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/maintenance/renew", StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            return respond(request);
        }
    }

    // Starts a session with a session-less dispose helper so the poll test can dispose a nullable result.
    private static void ServerMaintenanceSession_Dispose(ServerMaintenanceClient.ServerMaintenanceSession? session)
    {
        session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

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

            return path == "/api/v1/database/maintenance/begin"
                ? Json(HttpStatusCode.OK, beginBody ?? $"{{\"holdToken\":\"{HoldToken}\"}}")
                : path == "/api/v1/database/maintenance/renew"
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : path == "/api/v1/database/maintenance/end"
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static FakeDelay NewDelay()
    {
        return new();
    }

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

    // Moves only when the test advances it, so the 25-minute rule is tested without real waiting.
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by)
        {
            _now += by;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met within 5 seconds.");
            }

            await Task.Delay(10);
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

        public List<string> Messages { get; } = [];

        public bool IsInteractive => interactive;

        public void Write(string message)
        {
            Messages.Add(message);
        }

        public Task<bool> ConfirmAsync(string question, CancellationToken ct)
        {
            Questions.Add(question);
            return Task.FromResult(answer);
        }

        public bool OpenBrowser(Uri url)
        {
            return false;
        }
    }
}
