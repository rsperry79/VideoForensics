using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
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
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Exercises the real routing, authorization policy, and rate-limiting of the operator UI-mode endpoints
    /// through an in-memory TestServer, so policy wiring and authorization enforcement are proven rather than assumed.
    /// </summary>
    public class OperatorUiModeEndpointPipelineTests
    {
        private const string TestScheme = "UiModeTest";
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
            public required Mock<IOperatorRepository> Operators { get; init; }
            public required Mock<IOperatorPreferencesRepository> Preferences { get; init; }
            public required Mock<ISecurityAuditLogger> AuditLog { get; init; }
            public required List<string> AuditDetails { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<TestHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var operators = new Mock<IOperatorRepository>();
            var preferences = new Mock<IOperatorPreferencesRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var auditDetails = new List<string>();

            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<string, Guid?, Guid?, string, string, bool, CancellationToken>((_, _, _, _, d, _, _) => { lock (auditDetails) { auditDetails.Add(d); } })
                .Returns(Task.CompletedTask);

            var tier = new Mock<INetworkTierResolver>();
            _ = tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            _ = builder.Services.AddSingleton(operators.Object);
            _ = builder.Services.AddSingleton(preferences.Object);
            _ = builder.Services.AddSingleton(auditLog.Object);
            _ = builder.Services.AddSingleton(tier.Object);
            // Other routes mapped by MapDeviceManagementEndpoints need these resolvable, or endpoint construction throws at startup.
            _ = builder.Services.AddSingleton(new Mock<IPairedDeviceRepository>().Object);
            _ = builder.Services.AddSingleton(new Mock<IOperatorCredentialRepository>().Object);
            _ = builder.Services.AddSingleton(new Mock<VideoForensics.WebApp.Hubs.ILiveConnectionTracker>().Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            _ = builder.Services.AddRateLimiter(options =>
            {
                _ = options.AddPolicy("auth", httpContext =>
                    RateLimitPartition.GetNoLimiter("test"));
            });

            WebApplication app = builder.Build();
            _ = app.UseRateLimiter();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapDeviceManagementEndpoints();
            await app.StartAsync();

            return new TestHost
            {
                App = app,
                Client = app.GetTestClient(),
                Operators = operators,
                Preferences = preferences,
                AuditLog = auditLog,
                AuditDetails = auditDetails
            };
        }

        private static HttpRequestMessage Get(string url, Action<HttpRequestMessage>? configure = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            configure?.Invoke(req);
            return req;
        }

        private static HttpRequestMessage Put(string url, SetOperatorUiModeRequest body, Action<HttpRequestMessage>? configure = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Content = JsonContent.Create(body, mediaType: System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json"));
            configure?.Invoke(req);
            return req;
        }

        // ---- authorization tests ---------------------------------------------------------------

        [Fact]
        public async Task GetUiMode_AdminRole_LocalTier_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorPreferences?)null);

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<OperatorUiModeDto>(WebJson);
            Assert.NotNull(dto);
            Assert.Equal("Standard", dto.Mode);
            Assert.False(dto.Locked);
        }

        [Fact]
        public async Task PutUiMode_AdminRole_LocalTier_ValidMode_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            var savedPrefs = new OperatorPreferences
            {
                OperatorId = targetId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "System"
            };
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.SetUiModeAsync(targetId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPrefs);

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<OperatorUiModeDto>(WebJson);
            Assert.NotNull(dto);
            Assert.Equal("Simple", dto.Mode);
            Assert.True(dto.Locked);
            host.AuditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.OperatorUiModeChanged,
                It.IsAny<Guid?>(), null, "127.0.0.1",
                It.Is<string>(d => d.Contains("mode=Simple") && d.Contains("locked=True")),
                false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetUiMode_SuperAdminRole_LocalTier_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorPreferences?)null);

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Role", "SuperAdmin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        [Fact]
        public async Task PutUiMode_SuperAdminRole_LocalTier_ValidMode_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            var savedPrefs = new OperatorPreferences
            {
                OperatorId = targetId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "System"
            };
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.SetUiModeAsync(targetId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPrefs);

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Role", "SuperAdmin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        [Theory]
        [InlineData("Review")]
        [InlineData("ReadOnly")]
        public async Task GetUiMode_NonAdminRole_Returns403(string role)
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Role", role)));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.AuditLog.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("Review")]
        [InlineData("ReadOnly")]
        public async Task PutUiMode_NonAdminRole_Returns403(string role)
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Role", role)));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.AuditLog.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetUiMode_Unauthenticated_Returns401()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Anonymous", "1")));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task PutUiMode_Unauthenticated_Returns401()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Anonymous", "1")));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task GetUiMode_AdminRole_NetworkTier_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorPreferences?)null);

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r =>
            {
                r.Headers.Add("X-Test-Role", "Admin");
                r.Headers.Add("X-Test-Tier", "Network");
            }));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        [Fact]
        public async Task PutUiMode_AdminRole_NetworkTier_Returns200()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            var savedPrefs = new OperatorPreferences
            {
                OperatorId = targetId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "System"
            };
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.SetUiModeAsync(targetId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPrefs);

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r =>
            {
                r.Headers.Add("X-Test-Role", "Admin");
                r.Headers.Add("X-Test-Tier", "Network");
            }));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        [Fact]
        public async Task PutUiMode_InvalidMode_Returns400_NoAuditLog()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("InvalidMode", false), r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            host.AuditLog.VerifyNoOtherCalls();
        }

        // ---- scope, audit and not-found tests --------------------------------------------------

        private static string UnlockUrl(Guid id) => $"/api/devices-management/operators/{id}/unlock";

        [Theory]
        [InlineData("Admin", "Local")]
        [InlineData("Review", "Local")]
        [InlineData("SuperAdmin", "Network")]
        public async Task UnlockOperator_NotSuperAdminLocal_Returns403_AndDoesNotUnlock(string role, string tier)
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, UnlockUrl(targetId))
            {
                Headers = { { "X-Test-Role", role }, { "X-Test-Tier", tier } }
            });

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Operators.Verify(r => r.UnlockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UnlockOperator_SuperAdminLocal_Returns200_AndUnlocks()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();

            var resp = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, UnlockUrl(targetId))
            {
                Headers = { { "X-Test-Role", "SuperAdmin" }, { "X-Test-Tier", "Local" } }
            });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Operators.Verify(r => r.UnlockAsync(targetId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PutUiMode_Valid_WritesExactlyOneAuditEntryWithTargetModeAndLocked()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });
            host.Preferences.Setup(r => r.SetUiModeAsync(targetId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperatorPreferences { OperatorId = targetId, UiMode = "Simple", UiModeLocked = true, ThemeMode = "System" });

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.AuditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            host.AuditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.OperatorUiModeChanged, It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            string details = Assert.Single(host.AuditDetails);
            Assert.Contains("target=target", details);
            Assert.Contains("mode=Simple", details);
            Assert.Contains("locked=True", details);
        }

        [Fact]
        public async Task GetUiMode_Valid_WritesNoAuditEntry()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = targetId, Username = "target", DisplayName = "Target", FirstName = "T", LastName = "Op", Email = "t@ex.com" });

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.AuditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(host.AuditDetails);
        }

        [Fact]
        public async Task PutUiMode_UnknownOperator_Returns404_NoWriteNoAudit()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var resp = await host.Client.SendAsync(Put($"/api/devices-management/operators/{targetId}/ui-mode", new SetOperatorUiModeRequest("Simple", true), r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
            host.Preferences.Verify(r => r.SetUiModeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            host.AuditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetUiMode_UnknownOperator_Returns404()
        {
            await using var host = await StartAsync();
            var targetId = Guid.NewGuid();
            host.Operators.Setup(r => r.GetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var resp = await host.Client.SendAsync(Get($"/api/devices-management/operators/{targetId}/ui-mode", r => r.Headers.Add("X-Test-Role", "Admin")));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }
    }
}