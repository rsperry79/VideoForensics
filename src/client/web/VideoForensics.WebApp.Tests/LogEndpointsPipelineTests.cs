using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.DependencyInjection;
using VideoForensics.Core.Logging.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Exercises the real routing, authorization policy, step-up filter and SSE response bytes of the log endpoints
    /// through an in-memory TestServer, so framing and policy wiring are proven rather than assumed.
    /// </summary>
    public class LogEndpointsPipelineTests
    {
        private const string TestScheme = "LogTest";
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (Request.Headers.ContainsKey("X-Test-Anonymous"))
                    return Task.FromResult(AuthenticateResult.NoResult());

                string role = Request.Headers.TryGetValue("X-Test-Role", out var r) ? r.ToString() : "SuperAdmin";
                string tier = Request.Headers.TryGetValue("X-Test-Tier", out var t) ? t.ToString() : "Local";
                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, role),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, tier),
                    new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
                ], TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        private sealed class TestHost : IAsyncDisposable
        {
            public required WebApplication App { get; init; }
            public required HttpClient Client { get; init; }
            public required InMemoryLogBuffer Buffer { get; init; }
            public required Mock<ISecurityAuditLogger> Audit { get; init; }
            public required List<string> AuditDetails { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<TestHost> StartAsync(TimeSpan? heartbeat = null, bool stepUpValid = true)
        {
            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var buffer = new InMemoryLogBuffer(capacity: 5000);
            var audit = new Mock<ISecurityAuditLogger>();
            var details = new List<string>();
            _ = audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<string, Guid?, Guid?, string, string, bool, CancellationToken>((_, _, _, _, d, _, _) => { lock (details) { details.Add(d); } })
                .Returns(Task.CompletedTask);

            var stepUp = new Mock<IStepUpAuthService>();
            _ = stepUp.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<Guid>())).Returns(stepUpValid);

            var tier = new Mock<INetworkTierResolver>();
            _ = tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            _ = builder.Services.AddSingleton(buffer);
            _ = builder.Services.AddSingleton(audit.Object);
            _ = builder.Services.AddSingleton(stepUp.Object);
            _ = builder.Services.AddSingleton(tier.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            if (heartbeat.HasValue)
            {
                _ = builder.Services.Configure<LogStreamOptions>(o => o.HeartbeatInterval = heartbeat.Value);
            }

            WebApplication app = builder.Build();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapLogEndpoints();
            await app.StartAsync();

            return new TestHost { App = app, Client = app.GetTestClient(), Buffer = buffer, Audit = audit, AuditDetails = details };
        }

        private static HttpRequestMessage Get(string url, string? stepUp = "valid", Action<HttpRequestMessage>? configure = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (stepUp != null)
                req.Headers.Add("X-StepUp-Token", stepUp);
            configure?.Invoke(req);
            return req;
        }

        private static void Add(InMemoryLogBuffer buffer, string level = "Information", string message = "msg", string category = "Cat")
        {
            var r = new LogRecord(0, DateTimeOffset.UtcNow, level, category, message, null);
            buffer.Append(ref r);
        }

        private static async Task<(HttpResponseMessage Response, StreamReader Reader)> OpenStreamAsync(TestHost host, string url, CancellationToken ct, Action<HttpRequestMessage>? configure = null)
        {
            var response = await host.Client.SendAsync(Get(url, "valid", configure), HttpCompletionOption.ResponseHeadersRead, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (response, new StreamReader(await response.Content.ReadAsStreamAsync(ct)));
        }

        /// <summary>Reads lines up to and including the blank line that terminates one SSE frame.</summary>
        private static async Task<List<string>> ReadFrameAsync(StreamReader reader)
        {
            using var cts = new CancellationTokenSource(ReadTimeout);
            var lines = new List<string>();
            while (true)
            {
                string? line = await reader.ReadLineAsync(cts.Token);
                Assert.NotNull(line);
                if (line.Length == 0)
                    return lines;
                lines.Add(line);
            }
        }

        private static async Task<List<string>> ReadDataFrameAsync(StreamReader reader)
        {
            while (true)
            {
                var frame = await ReadFrameAsync(reader);
                if (!frame.All(l => l.StartsWith(':')))
                    return frame;
            }
        }

        private static LogEntryDto ParseData(List<string> frame)
        {
            string data = Assert.Single(frame, l => l.StartsWith("data:"));
            Assert.StartsWith("data: {", data);
            return JsonSerializer.Deserialize<LogEntryDto>(data["data: ".Length..], WebJson)!;
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + ReadTimeout;
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "Condition not met in time");
                await Task.Delay(10);
            }
        }

        // ---- authorization / step-up ----------------------------------------------------------------

        [Theory]
        [InlineData("/api/v1/logs")]
        [InlineData("/api/v1/logs/stream")]
        public async Task Logs_NonSuperAdminRole_IsForbidden(string url)
        {
            await using var host = await StartAsync();
            var resp = await host.Client.SendAsync(Get(url, "valid", r => r.Headers.Add("X-Test-Role", "Admin")), HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Audit.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("/api/v1/logs")]
        [InlineData("/api/v1/logs/stream")]
        public async Task Logs_NonLocalTier_IsForbidden(string url)
        {
            await using var host = await StartAsync();
            var resp = await host.Client.SendAsync(Get(url, "valid", r => r.Headers.Add("X-Test-Tier", "Network")), HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Audit.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("/api/v1/logs")]
        [InlineData("/api/v1/logs/stream")]
        public async Task Logs_Unauthenticated_IsUnauthorized(string url)
        {
            await using var host = await StartAsync();
            var resp = await host.Client.SendAsync(Get(url, "valid", r => r.Headers.Add("X-Test-Anonymous", "1")), HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Theory]
        [InlineData("/api/v1/logs")]
        [InlineData("/api/v1/logs/stream")]
        public async Task Logs_MissingStepUpToken_IsForbidden(string url)
        {
            await using var host = await StartAsync();
            var resp = await host.Client.SendAsync(Get(url, stepUp: null), HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Audit.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("/api/v1/logs")]
        [InlineData("/api/v1/logs/stream")]
        public async Task Logs_InvalidStepUpToken_IsForbidden(string url)
        {
            await using var host = await StartAsync(stepUpValid: false);
            var resp = await host.Client.SendAsync(Get(url), HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Audit.VerifyNoOtherCalls();
        }

        // ---- audit ----------------------------------------------------------------------------------

        [Fact]
        public async Task GetLogs_ValidCall_AuditsExactlyOnceWithoutSearchText()
        {
            await using var host = await StartAsync();
            Add(host.Buffer, message: "contains distinctive-needle-xyz");

            var resp = await host.Client.SendAsync(Get("/api/v1/logs?search=distinctive-needle-xyz&minLevel=Information"));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Audit.Verify(a => a.LogAsync(SecurityAuditEventTypes.LogViewed, It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            host.Audit.VerifyNoOtherCalls();
            string details = Assert.Single(host.AuditDetails);
            Assert.DoesNotContain("distinctive-needle-xyz", details);
            Assert.DoesNotContain("contains", details);
        }

        [Fact]
        public async Task StreamLogs_Open_AuditsExactlyOnceWithoutSearchText()
        {
            await using var host = await StartAsync();
            Add(host.Buffer, message: "contains distinctive-needle-xyz");
            using var cts = new CancellationTokenSource(ReadTimeout);

            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream?search=distinctive-needle-xyz", cts.Token);
            _ = await ReadDataFrameAsync(reader);

            host.Audit.Verify(a => a.LogAsync(SecurityAuditEventTypes.LogViewed, It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            host.Audit.VerifyNoOtherCalls();
            string details = Assert.Single(host.AuditDetails);
            Assert.DoesNotContain("distinctive-needle-xyz", details);
            resp.Dispose();
        }

        // ---- limit clamping (history) ---------------------------------------------------------------

        [Theory]
        [InlineData(null, 500)]
        [InlineData(0, 500)]
        [InlineData(-5, 500)]
        [InlineData(1, 1)]
        [InlineData(2000, 2000)]
        [InlineData(99999, 2000)]
        public async Task GetLogs_Limit_IsClamped(int? limit, int expectedCount)
        {
            await using var host = await StartAsync();
            for (int i = 0; i < 2100; i++)
                Add(host.Buffer);

            string url = limit.HasValue ? $"/api/v1/logs?limit={limit}" : "/api/v1/logs";
            var resp = await host.Client.SendAsync(Get(url));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var page = await resp.Content.ReadFromJsonAsync<LogPageDto>(WebJson);

            Assert.NotNull(page);
            Assert.Equal(expectedCount, page.Entries.Count);
            Assert.True(page.Truncated);
        }

        // ---- SSE ------------------------------------------------------------------------------------

        [Fact]
        public async Task StreamLogs_Backlog_UsesSseIdAndSingleDataLine()
        {
            await using var host = await StartAsync();
            Add(host.Buffer, message: "one");
            Add(host.Buffer, message: "two");
            using var cts = new CancellationTokenSource(ReadTimeout);

            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream", cts.Token);

            Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);
            var f1 = await ReadDataFrameAsync(reader);
            Assert.Contains("id: 1", f1);
            Assert.Equal("one", ParseData(f1).Message);
            var f2 = await ReadDataFrameAsync(reader);
            Assert.Contains("id: 2", f2);
            Assert.Equal("two", ParseData(f2).Message);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_EntryLoggedAfterOpen_IsDelivered()
        {
            await using var host = await StartAsync();
            Add(host.Buffer, message: "old");
            using var cts = new CancellationTokenSource(ReadTimeout);
            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream", cts.Token);
            _ = await ReadDataFrameAsync(reader);

            Add(host.Buffer, message: "fresh");
            Add(host.Buffer, message: "fresher");

            var f = await ReadDataFrameAsync(reader);
            Assert.Contains("id: 2", f);
            Assert.Equal("fresh", ParseData(f).Message);
            Assert.Equal(3, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_LastEventId_ResumesWithoutDuplicates()
        {
            await using var host = await StartAsync();
            for (int i = 0; i < 3; i++)
                Add(host.Buffer);
            using var cts = new CancellationTokenSource(ReadTimeout);

            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream", cts.Token, r => r.Headers.Add("Last-Event-ID", "2"));

            Assert.Equal(3, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            Add(host.Buffer);
            Assert.Equal(4, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_MinLevelAndSearch_ApplyToLiveEntries()
        {
            await using var host = await StartAsync();
            using var cts = new CancellationTokenSource(ReadTimeout);
            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream?minLevel=Warning&search=needle", cts.Token);
            await WaitForAsync(() => host.Buffer.SubscriberCount == 1);

            Add(host.Buffer, "Information", "needle but too quiet");
            Add(host.Buffer, "Error", "loud but no match");
            Add(host.Buffer, "Error", "loud NEEDLE");

            Assert.Equal(3, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_NoAfterSequence_BacklogCappedToNewestLimit()
        {
            await using var host = await StartAsync();
            for (int i = 0; i < 10; i++)
                Add(host.Buffer);
            using var cts = new CancellationTokenSource(ReadTimeout);

            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream?limit=3", cts.Token);

            Assert.Equal(8, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            Assert.Equal(9, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            Assert.Equal(10, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            resp.Dispose();
        }

        [Theory]
        [InlineData(null, 500)]
        [InlineData(0, 500)]
        [InlineData(-5, 500)]
        [InlineData(99999, 2000)]
        public async Task StreamLogs_Limit_DefaultsAndClampsBacklogCap(int? limit, int expectedBacklogSize)
        {
            await using var host = await StartAsync();
            for (int i = 0; i < 2100; i++)
                Add(host.Buffer);
            using var cts = new CancellationTokenSource(ReadTimeout);

            string url = limit.HasValue ? $"/api/v1/logs/stream?limit={limit}" : "/api/v1/logs/stream";
            var (resp, reader) = await OpenStreamAsync(host, url, cts.Token);

            Assert.Equal(2100 - expectedBacklogSize + 1, ParseData(await ReadDataFrameAsync(reader)).Sequence);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_Idle_SendsHeartbeatCommentFrames()
        {
            await using var host = await StartAsync(heartbeat: TimeSpan.FromMilliseconds(50));
            using var cts = new CancellationTokenSource(ReadTimeout);
            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream", cts.Token);

            var frame = await ReadFrameAsync(reader);

            Assert.Equal([": heartbeat"], frame);
            resp.Dispose();
        }

        [Fact]
        public async Task StreamLogs_ClientDisconnects_RemovesSubscriber()
        {
            await using var host = await StartAsync(heartbeat: TimeSpan.FromMilliseconds(50));
            using var cts = new CancellationTokenSource(ReadTimeout);
            var (resp, reader) = await OpenStreamAsync(host, "/api/v1/logs/stream", cts.Token);
            _ = await ReadFrameAsync(reader);
            Assert.Equal(1, host.Buffer.SubscriberCount);

            reader.Dispose();
            resp.Dispose();

            await WaitForAsync(() => host.Buffer.SubscriberCount == 0);
        }

        // ---- logging wiring -------------------------------------------------------------------------

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddVideoForensicsLogging_BufferRegistration_LoggedLineAppearsInSameBuffer(bool registerBufferFirst)
        {
            var services = new ServiceCollection();
            if (registerBufferFirst)
                _ = services.AddInMemoryLogBuffer();
            _ = services.AddLogging(b => b.AddVideoForensicsLogging("", LogLevel.Information, enableEventLog: false, enableSyslog: false));
            if (!registerBufferFirst)
                _ = services.AddInMemoryLogBuffer();

            using var sp = services.BuildServiceProvider();
            var buffer = sp.GetRequiredService<InMemoryLogBuffer>();
            sp.GetRequiredService<ILogger<LogEndpointsPipelineTests>>().LogWarning("wiring-marker-{Id}", 42);

            var entries = buffer.GetSnapshot(null, "wiring-marker-42", null, 10);
            Assert.Single(entries);
        }
    }
}
