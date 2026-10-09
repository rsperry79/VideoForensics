using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Exercises the live-view REST routes through an in-memory TestServer so routing, the Admin/ReadOnly
    /// policies, the media rate-limit policy, the operator-claim check and the exception-to-status mapping
    /// are all proven through the real pipeline. <see cref="ILiveViewSessionService"/> is mocked.
    /// </summary>
    public class LiveViewEndpointsTests
    {
        private const string TestScheme = "LiveViewTest";
        private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid OperatorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid SessionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (Request.Headers.ContainsKey("X-Test-Anonymous"))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                string role = Request.Headers.TryGetValue("X-Test-Role", out var r) ? r.ToString() : "Admin";
                var claims = new List<Claim> { new(VideoForensicsClaimTypes.Role, role) };

                if (Request.Headers.TryGetValue("X-Test-Operator", out var op))
                {
                    claims.Add(new Claim(VideoForensicsClaimTypes.OperatorId, op.ToString()));
                }
                else if (!Request.Headers.ContainsKey("X-Test-No-Operator"))
                {
                    claims.Add(new Claim(VideoForensicsClaimTypes.OperatorId, OperatorId.ToString()));
                }

                var identity = new ClaimsIdentity(claims, TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        private sealed class LiveViewHost : IAsyncDisposable
        {
            public required WebApplication App { get; init; }
            public required HttpClient Client { get; init; }
            public required Mock<ILiveViewSessionService> Service { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<LiveViewHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var service = new Mock<ILiveViewSessionService>();
            _ = builder.Services.AddSingleton(service.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            _ = builder.Services.AddRateLimiter(options =>
            {
                _ = options.AddPolicy("media", _ => RateLimitPartition.GetNoLimiter("test"));
            });

            WebApplication app = builder.Build();
            _ = app.UseRateLimiter();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapLiveViewEndpoints();
            await app.StartAsync();

            return new LiveViewHost
            {
                App = app,
                Client = app.GetTestClient(),
                Service = service
            };
        }

        private static LiveViewSession SampleSession(LiveViewSessionState state = LiveViewSessionState.Active) => new()
        {
            Id = SessionId,
            DeviceId = DeviceId,
            TriggerReason = LiveViewTriggerReason.Manual,
            State = state,
            StartedAtUtc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
            LastExtendedAtUtc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
            OperatorId = OperatorId
        };

        private static HttpRequestMessage Req(HttpMethod method, string url, string? role = null, bool anonymous = false, bool noOperator = false, HttpContent? content = null)
        {
            var req = new HttpRequestMessage(method, url) { Content = content };
            if (role != null)
            {
                req.Headers.Add("X-Test-Role", role);
            }

            if (anonymous)
            {
                req.Headers.Add("X-Test-Anonymous", "1");
            }

            if (noOperator)
            {
                req.Headers.Add("X-Test-No-Operator", "1");
            }

            return req;
        }

        private static JsonContent StartBody(string reason = "Manual") => JsonContent.Create(new StartLiveViewRequestDto(DeviceId, reason));

        private static string StartUrl => "/api/v1/live-view/start";
        private static string ExtendUrl => $"/api/v1/live-view/{SessionId}/extend";
        private static string PromoteUrl => $"/api/v1/live-view/{SessionId}/promote";
        private static string DemoteUrl => $"/api/v1/live-view/{SessionId}/demote";
        private static string StopUrl => $"/api/v1/live-view/{SessionId}/stop";
        private static string ActiveUrl => $"/api/v1/live-view/device/{DeviceId}/active";

        // ---- start ----------------------------------------------------------------------------------

        [Fact]
        public async Task Start_ValidRequest_Returns200WithSessionDto()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.StartAsync(DeviceId, LiveViewTriggerReason.Manual, OperatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SampleSession());

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody()));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            LiveViewSessionDto? dto = await resp.Content.ReadFromJsonAsync<LiveViewSessionDto>();
            Assert.NotNull(dto);
            Assert.Equal(SessionId, dto!.Id);
            Assert.Equal("Manual", dto.TriggerReason);
            Assert.Equal("Active", dto.State);
            host.Service.Verify(s => s.StartAsync(DeviceId, LiveViewTriggerReason.Manual, OperatorId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Start_UnknownReason_Returns400()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody("NotAReason")));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            host.Service.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Start_NumericReason_Returns400()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody("99")));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        [Fact]
        public async Task Start_MissingOperatorClaim_Returns401()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", noOperator: true, content: StartBody()));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
            host.Service.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Start_UnparseableOperatorClaim_Returns401()
        {
            await using var host = await StartAsync();
            var req = Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody());
            req.Headers.Add("X-Test-Operator", "not-a-guid");

            HttpResponseMessage resp = await host.Client.SendAsync(req);

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task Start_ServiceNotFound_Returns404()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody()));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task Start_ServiceInvalidOperation_Returns409()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Live view is disabled."));

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody()));

            Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        }

        [Fact]
        public async Task Start_ServiceNotSupported_Returns501()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException());

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "Admin", content: StartBody()));

            Assert.Equal(HttpStatusCode.NotImplemented, resp.StatusCode);
        }

        // ---- extend ---------------------------------------------------------------------------------

        [Fact]
        public async Task Extend_Valid_Returns204()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, ExtendUrl, "Admin"));

            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
            host.Service.Verify(s => s.ExtendAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Extend_UnknownSession_Returns404()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.ExtendAsync(SessionId, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, ExtendUrl, "Admin"));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        // ---- promote / demote -----------------------------------------------------------------------

        [Fact]
        public async Task Promote_Valid_Returns200WithDto()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.PromoteToSustainedAsync(SessionId, "Sustained bitrate degradation", It.IsAny<CancellationToken>()))
                .ReturnsAsync(SampleSession(LiveViewSessionState.Sustained));

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, PromoteUrl, "Admin",
                content: JsonContent.Create("Sustained bitrate degradation")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            LiveViewSessionDto? dto = await resp.Content.ReadFromJsonAsync<LiveViewSessionDto>();
            Assert.Equal("Sustained", dto!.State);
            host.Service.Verify(s => s.PromoteToSustainedAsync(SessionId, "Sustained bitrate degradation", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Demote_Valid_Returns200WithDto()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.DemoteFromSustainedAsync(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SampleSession(LiveViewSessionState.Active));

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, DemoteUrl, "Admin"));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            LiveViewSessionDto? dto = await resp.Content.ReadFromJsonAsync<LiveViewSessionDto>();
            Assert.Equal("Active", dto!.State);
        }

        // ---- stop -----------------------------------------------------------------------------------

        [Fact]
        public async Task Stop_Valid_Returns204()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StopUrl, "Admin",
                content: JsonContent.Create(new StopLiveViewRequestDto("ManualStop"))));

            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
            host.Service.Verify(s => s.StopAsync(SessionId, "ManualStop", It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---- active lookup --------------------------------------------------------------------------

        [Fact]
        public async Task GetActive_NoSession_Returns404()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.GetActiveSessionAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync((LiveViewSession?)null);

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, ActiveUrl, "ReadOnly"));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task GetActive_Session_Returns200WithDto()
        {
            await using var host = await StartAsync();
            _ = host.Service.Setup(s => s.GetActiveSessionAsync(DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(SampleSession());

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, ActiveUrl, "ReadOnly"));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            LiveViewSessionDto? dto = await resp.Content.ReadFromJsonAsync<LiveViewSessionDto>();
            Assert.Equal(SessionId, dto!.Id);
            Assert.Equal(DeviceId, dto.DeviceId);
        }

        [Fact]
        public async Task GetActive_Anonymous_Returns401()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, ActiveUrl, anonymous: true));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // ---- authorization --------------------------------------------------------------------------

        [Fact]
        public async Task Start_ReadOnlyRole_Returns403()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StartUrl, "ReadOnly", content: StartBody()));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Service.Verify(s => s.StartAsync(It.IsAny<Guid>(), It.IsAny<LiveViewTriggerReason>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Stop_ReadOnlyRole_Returns403()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, StopUrl, "ReadOnly",
                content: JsonContent.Create(new StopLiveViewRequestDto("ManualStop"))));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Service.Verify(s => s.StopAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("extend")]
        [InlineData("promote")]
        [InlineData("demote")]
        public async Task Mutations_ReviewRole_Returns403(string action)
        {
            await using var host = await StartAsync();
            string url = action switch
            {
                "extend" => ExtendUrl,
                "promote" => PromoteUrl,
                _ => DemoteUrl
            };

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, url, "Review",
                content: action == "promote" ? JsonContent.Create("reason") : null));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        }

        // ---- routing --------------------------------------------------------------------------------

        [Fact]
        public async Task Routes_AreVersioned_UnderApiV1LiveView()
        {
            await using var host = await StartAsync();

            IEnumerable<string> patterns = host.App.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(e => e.RoutePattern.RawText ?? string.Empty);

            List<string> all = patterns.ToList();
            Assert.NotEmpty(all);
            Assert.All(all, p => Assert.StartsWith("/api/v1/live-view/", p));
        }
    }
}
