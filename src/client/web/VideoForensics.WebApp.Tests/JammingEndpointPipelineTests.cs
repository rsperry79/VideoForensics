using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
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
    /// Exercises the real routing, authorization policies and rate-limiter wiring of the jamming API
    /// through an in-memory TestServer, so "ReadOnly may read, Admin may write" is proven, not assumed.
    /// </summary>
    public class JammingEndpointPipelineTests
    {
        private const string TestScheme = "JammingTest";
        private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
            public required Mock<IJammingRepository> Jamming { get; init; }
            public required Mock<IDeviceRepository> Devices { get; init; }
            public required Mock<ISecurityAuditLogger> AuditLog { get; init; }
            public required List<JammingIncidentRecord> Upserted { get; init; }

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

            var jamming = new Mock<IJammingRepository>();
            var devices = new Mock<IDeviceRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var upserted = new List<JammingIncidentRecord>();

            _ = devices.Setup(d => d.GetAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Device { Id = DeviceId, ProviderDeviceId = "p", Name = "Front Door", Type = "doorbell" });
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = jamming.Setup(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingIncidentRecord r, CancellationToken _) =>
                {
                    if (r.Id == Guid.Empty)
                    {
                        r.Id = Guid.NewGuid();
                    }

                    lock (upserted) { upserted.Add(r); }
                    return r;
                });
            _ = jamming.Setup(j => j.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = jamming.Setup(j => j.ListStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = jamming.Setup(j => j.RecomputeStatsAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { Id = Guid.NewGuid(), DeviceId = DeviceId, IncidentCount = 1 });

            var tier = new Mock<INetworkTierResolver>();
            _ = tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            _ = builder.Services.AddSingleton(jamming.Object);
            _ = builder.Services.AddSingleton(devices.Object);
            _ = builder.Services.AddSingleton(auditLog.Object);
            _ = builder.Services.AddSingleton(tier.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });
            _ = builder.Services.AddRateLimiter(options =>
            {
                _ = options.AddPolicy("default", httpContext => RateLimitPartition.GetNoLimiter("test"));
            });

            WebApplication app = builder.Build();
            _ = app.UseRateLimiter();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapJammingEndpoints();
            await app.StartAsync();

            return new TestHost
            {
                App = app,
                Client = app.GetTestClient(),
                Jamming = jamming,
                Devices = devices,
                AuditLog = auditLog,
                Upserted = upserted
            };
        }

        private static HttpRequestMessage Req(HttpMethod method, string url, string? role = null, bool anonymous = false, HttpContent? content = null)
        {
            var req = new HttpRequestMessage(method, url) { Content = content };
            if (role != null)
                req.Headers.Add("X-Test-Role", role);
            if (anonymous)
                req.Headers.Add("X-Test-Anonymous", "1");
            return req;
        }

        private static JsonContent ValidBody(Guid? deviceId = null) => JsonContent.Create(new UpsertJammingIncidentRequest(
            Guid.Empty, deviceId ?? DeviceId,
            new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc),
            3, 14.5, "High", "note"));

        // ---- reads: any signed-in role -------------------------------------------------------------

        [Theory]
        [InlineData("ReadOnly")]
        [InlineData("Review")]
        [InlineData("Admin")]
        [InlineData("SuperAdmin")]
        public async Task Reads_AnySignedInRole_Return200(string role)
        {
            await using var host = await StartAsync();
            var incidentId = Guid.NewGuid();
            _ = host.Jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingIncidentRecord { Id = incidentId, DeviceId = DeviceId });
            _ = host.Jamming.Setup(j => j.GetStatsAsync(DeviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JammingStatsSummary { DeviceId = DeviceId });

            foreach (string url in new[] { "/api/v1/jamming/incidents", $"/api/v1/jamming/incidents/{incidentId}", $"/api/v1/jamming/stats/{DeviceId}", "/api/v1/jamming/stats" })
            {
                HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, url, role));
                Assert.True(resp.StatusCode == HttpStatusCode.OK, $"{role} GET {url} => {(int)resp.StatusCode}");
            }
        }

        [Theory]
        [InlineData("/api/v1/jamming/incidents")]
        [InlineData("/api/v1/jamming/stats")]
        public async Task Reads_Unauthenticated_Return401(string url)
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, url, anonymous: true));

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        [Fact]
        public async Task GetIncident_Unknown_Returns404()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/jamming/incidents/{Guid.NewGuid()}", "ReadOnly"));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task GetStats_NoneForDevice_Returns404()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/jamming/stats/{DeviceId}", "ReadOnly"));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task ListIncidents_QueryFiltersBindAndReachRepositoryAsUtc()
        {
            await using var host = await StartAsync();
            DateTime from = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime to = new(2026, 3, 2, 12, 30, 0, DateTimeKind.Utc);
            string url = $"/api/v1/jamming/incidents?deviceId={DeviceId}&fromUtc={Uri.EscapeDataString(from.ToString("O"))}&toUtc={Uri.EscapeDataString(to.ToString("O"))}";

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Get, url, "ReadOnly"));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            host.Jamming.Verify(j => j.ListIncidentsAsync(DeviceId, It.Is<DateTime?>(d => d == from && d.Value.Kind == DateTimeKind.Utc), It.Is<DateTime?>(d => d == to && d.Value.Kind == DateTimeKind.Utc), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ListIncidents_NoQuery_PassesNullFilters()
        {
            await using var host = await StartAsync();

            _ = await host.Client.SendAsync(Req(HttpMethod.Get, "/api/v1/jamming/incidents", "ReadOnly"));

            host.Jamming.Verify(j => j.ListIncidentsAsync(null, null, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---- writes: Admin and above only ----------------------------------------------------------

        [Theory]
        [InlineData("ReadOnly")]
        [InlineData("Review")]
        public async Task Put_BelowAdmin_Returns403AndWritesNothing(string role)
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", role, content: ValidBody()));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
            host.AuditLog.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("ReadOnly")]
        [InlineData("Review")]
        public async Task Recompute_BelowAdmin_Returns403AndRecomputesNothing(string role)
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/jamming/stats/{DeviceId}/recompute", role));

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            host.Jamming.Verify(j => j.RecomputeStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("SuperAdmin")]
        public async Task Put_AdminAndSuperAdmin_Return200AndUpsertOnce(string role)
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", role, content: ValidBody()));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<JammingIncidentDto>(WebJson);
            Assert.NotNull(dto);
            Assert.Equal("ManuallyRecorded", dto.Source);
            Assert.NotEqual(Guid.Empty, dto.Id);
            host.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Once);
            host.AuditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.JammingIncidentRecorded, It.IsAny<Guid?>(), null, "127.0.0.1", It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("SuperAdmin")]
        public async Task Recompute_AdminAndSuperAdmin_Return200(string role)
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/jamming/stats/{DeviceId}/recompute", role));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<JammingStatsSummaryDto>(WebJson);
            Assert.Equal(1, dto!.IncidentCount);
        }

        [Fact]
        public async Task Writes_Unauthenticated_Return401()
        {
            await using var host = await StartAsync();

            HttpResponseMessage put = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", anonymous: true, content: ValidBody()));
            HttpResponseMessage post = await host.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/jamming/stats/{DeviceId}/recompute", anonymous: true));

            Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
            host.Jamming.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Put_AdminFromInternetTier_IsAllowed()
        {
            await using var host = await StartAsync();
            var req = Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: ValidBody());
            req.Headers.Add("X-Test-Tier", "Internet");

            HttpResponseMessage resp = await host.Client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        // ---- PUT behaviour through the real binder -------------------------------------------------

        [Fact]
        public async Task Put_BodyContainingCaseIdAndDetectedAt_AreIgnoredForNewIncident_SourceIsHonoured()
        {
            await using var host = await StartAsync();
            string json = $$"""
                {"id":"{{Guid.Empty}}","deviceId":"{{DeviceId}}","startUtc":"2026-03-01T10:00:00Z","endUtc":"2026-03-01T10:30:00Z",
                 "affectedEventCount":3,"averageDegradationDb":14.5,"confidence":"High","notes":"n",
                 "caseId":"{{Guid.NewGuid()}}","source":"AutoDetected","detectedAtUtc":"2001-01-01T00:00:00Z"}
                """;

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: new StringContent(json, Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            JammingIncidentRecord sent = Assert.Single(host.Upserted);
            Assert.Null(sent.CaseId);
            Assert.Equal(JammingIncidentSource.AutoDetected, sent.Source);
            Assert.True(sent.DetectedAtUtc > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var dto = await resp.Content.ReadFromJsonAsync<JammingIncidentDto>(WebJson);
            Assert.Null(dto!.CaseId);
            Assert.Equal("AutoDetected", dto.Source);
        }

        [Fact]
        public async Task Put_BodyWithoutSource_StoresManuallyRecorded()
        {
            await using var host = await StartAsync();
            string json = $$"""
                {"id":"{{Guid.Empty}}","deviceId":"{{DeviceId}}","startUtc":"2026-03-01T10:00:00Z","endUtc":"2026-03-01T10:30:00Z",
                 "affectedEventCount":3,"averageDegradationDb":14.5,"confidence":"High"}
                """;

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: new StringContent(json, Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Equal(JammingIncidentSource.ManuallyRecorded, Assert.Single(host.Upserted).Source);
        }

        [Fact]
        public async Task Put_BodyWithInvalidSource_Returns400AndWritesNothing()
        {
            await using var host = await StartAsync();
            string json = $$"""
                {"id":"{{Guid.Empty}}","deviceId":"{{DeviceId}}","startUtc":"2026-03-01T10:00:00Z","endUtc":"2026-03-01T10:30:00Z",
                 "affectedEventCount":3,"averageDegradationDb":14.5,"confidence":"High","source":"Forged"}
                """;

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: new StringContent(json, Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            host.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Put_BodyContainingCaseId_DoesNotOverrideStoredCaseIdOfExistingIncident()
        {
            await using var host = await StartAsync();
            var id = Guid.NewGuid();
            var storedCase = Guid.NewGuid();
            _ = host.Jamming.Setup(j => j.GetIncidentAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new JammingIncidentRecord
            {
                Id = id, DeviceId = DeviceId, CaseId = storedCase, Source = JammingIncidentSource.AutoDetected,
                DetectedAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            string json = $$"""
                {"id":"{{id}}","deviceId":"{{DeviceId}}","startUtc":"2026-03-01T10:00:00Z","endUtc":"2026-03-01T10:30:00Z",
                 "affectedEventCount":3,"averageDegradationDb":14.5,"confidence":"High","caseId":"{{Guid.NewGuid()}}","source":"ManuallyRecorded"}
                """;

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: new StringContent(json, Encoding.UTF8, "application/json")));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            JammingIncidentRecord sent = Assert.Single(host.Upserted);
            Assert.Equal(storedCase, sent.CaseId);
            Assert.Equal(JammingIncidentSource.AutoDetected, sent.Source);
        }

        [Fact]
        public async Task Put_InvalidBody_Returns400()
        {
            await using var host = await StartAsync();
            var body = JsonContent.Create(new UpsertJammingIncidentRequest(
                Guid.Empty, DeviceId, new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), 1, 1, "High", null));

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: body));

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            host.Jamming.Verify(j => j.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Put_UnknownDevice_Returns404NotServerError()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/jamming/incidents", "Admin", content: ValidBody(Guid.NewGuid())));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }

        [Fact]
        public async Task Recompute_UnknownDevice_Returns404()
        {
            await using var host = await StartAsync();

            HttpResponseMessage resp = await host.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/jamming/stats/{Guid.NewGuid()}/recompute", "Admin"));

            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        }
    }
}