using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Moq;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Runs the media-flag routes through the real authorization policies, so "Review may keep or dismiss, only Admin may reopen,
    /// and the operator is the token's" is proven through the pipeline rather than assumed.
    /// </summary>
    public class MediaFlagEndpointPipelineTests
    {
        private const string TestScheme = "MediaFlagTest";
        private static readonly Guid FlagId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

                string role = Request.Headers.TryGetValue("X-Test-Role", out StringValues r) ? r.ToString() : "Admin";
                string operatorId = Request.Headers.TryGetValue("X-Test-Operator", out StringValues o) ? o.ToString() : Guid.NewGuid().ToString();
                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, role),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, "Local"),
                    new Claim(VideoForensicsClaimTypes.OperatorId, operatorId)
                ], TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        private sealed class TestHost : IAsyncDisposable
        {
            public required WebApplication App { get; init; }
            public required HttpClient Client { get; init; }
            public required Mock<IMediaFlagRepository> Flags { get; init; }
            public required Mock<IMediaFlagChangeNotifier> Notifier { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<TestHost> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            var flag = new MediaFlag { Id = FlagId, DeviceId = DeviceId, FlagType = MediaFlagType.BitrateCollapse, Status = MediaFlagStatus.Kept };
            _ = flags.Setup(f => f.GetAsync(FlagId, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
            _ = flags.Setup(f => f.ListPageAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaFlagPage([], 0));
            _ = flags.Setup(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flag);
            _ = flags.Setup(f => f.ReopenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flag);

            _ = builder.Services.AddSingleton(flags.Object);
            _ = builder.Services.AddSingleton(notifier.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            _ = builder.Services.AddRateLimiter(options =>
            {
                _ = options.AddPolicy("media", httpContext => RateLimitPartition.GetNoLimiter("test"));
            });

            WebApplication app = builder.Build();
            _ = app.UseRateLimiter();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapMediaFlagEndpoints();
            await app.StartAsync();

            return new TestHost { App = app, Client = app.GetTestClient(), Flags = flags, Notifier = notifier };
        }

        private static HttpRequestMessage Req(HttpMethod method, string url, string? role = null, string? operatorId = null, HttpContent? content = null)
        {
            var req = new HttpRequestMessage(method, url) { Content = content };
            if (role != null)
            {
                req.Headers.Add("X-Test-Role", role);
            }

            if (operatorId != null)
            {
                req.Headers.Add("X-Test-Operator", operatorId);
            }

            return req;
        }

        private static string StatusBody(string newStatus) => $"{{\"expectedStatus\":\"Unreviewed\",\"newStatus\":\"{newStatus}\",\"reason\":\"checked\"}}";

        [Theory]
        [InlineData("ReadOnly")]
        [InlineData("Review")]
        [InlineData("Admin")]
        public async Task Reads_AnySignedInRole_Return200(string role)
        {
            await using TestHost host = await StartAsync();

            foreach (string url in new[] { "/api/v1/media-flags", "/api/v1/media-flags/count", $"/api/v1/media-flags/{FlagId}" })
            {
                HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, url, role));
                Assert.True(resp.StatusCode == HttpStatusCode.OK, $"{role} GET {url} => {(int)resp.StatusCode}");
            }
        }

        [Fact]
        public async Task Reads_Unauthenticated_Return401()
        {
            await using TestHost host = await StartAsync();

            var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/media-flags");
            req.Headers.Add("X-Test-Anonymous", "1");

            HttpResponseMessage resp = await host.Client.SendAsync(req);

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task Keep_ReadOnly_Returns403AndDoesNotWrite()
        {
            await using TestHost host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(
                Req(HttpMethod.Post, $"/api/v1/media-flags/{FlagId}/status", "ReadOnly", content: JsonContent.Create(new SetFlagStatusRequestDto("Unreviewed", "Kept", null))));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Flags.Verify(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("Review")]
        [InlineData("Admin")]
        public async Task Dismiss_ReviewOrAbove_Returns200(string role)
        {
            await using TestHost host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(
                Req(HttpMethod.Post, $"/api/v1/media-flags/{FlagId}/status", role, content: new StringContent(StatusBody("Dismissed"), System.Text.Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Notifier.Verify(n => n.MediaFlagChangedAsync(FlagId, DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Reopen_Review_Returns403()
        {
            await using TestHost host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(
                Req(HttpMethod.Post, $"/api/v1/media-flags/{FlagId}/reopen", "Review", content: JsonContent.Create(new ReopenFlagRequestDto("reason"))));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Flags.Verify(f => f.ReopenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Reopen_Admin_Returns200AndPublishes()
        {
            await using TestHost host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(
                Req(HttpMethod.Post, $"/api/v1/media-flags/{FlagId}/reopen", "Admin", content: JsonContent.Create(new ReopenFlagRequestDto("reason"))));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Notifier.Verify(n => n.MediaFlagChangedAsync(FlagId, DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Dismiss_OperatorIsTakenFromTokenNotBody()
        {
            await using TestHost host = await StartAsync();
            var tokenOperator = Guid.NewGuid();
            var bodyOperator = Guid.NewGuid();
            string body = $"{{\"expectedStatus\":\"Unreviewed\",\"newStatus\":\"Dismissed\",\"reason\":\"checked\",\"operatorId\":\"{bodyOperator}\"}}";

            HttpResponseMessage resp = await host.Client.SendAsync(
                Req(HttpMethod.Post, $"/api/v1/media-flags/{FlagId}/status", "Review", tokenOperator.ToString(), new StringContent(body, System.Text.Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Flags.Verify(f => f.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "checked", tokenOperator.ToString(), It.IsAny<CancellationToken>()), Times.Once);
            host.Flags.Verify(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), bodyOperator.ToString(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
