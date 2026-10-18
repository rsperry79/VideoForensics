using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Pipeline tests for POST sync-now (sync type and range) and GET/PUT config-sync-interval. Requests go through an
    /// in-memory TestServer so the real body binding and routing run; no network is used.
    /// </summary>
    public class AccountEndpointsSyncNowTests : IAsyncLifetime
    {
        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<IAccountSyncRunner> _runner = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IRingAccountSyncState> _syncState = new();
        private readonly TaskCompletionSource<AccountSyncRequest> _ranWith = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ranLegacy = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private WebApplication _app = null!;
        private HttpClient _client = null!;

        private string SyncNowUrl => $"/api/v1/accounts/provider-accounts/{_accountId}/sync-now";
        private string IntervalUrl => $"/api/v1/accounts/provider-accounts/{_accountId}/config-sync-interval";

        public async ValueTask InitializeAsync()
        {
            _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccount
                {
                    Id = _accountId,
                    UserId = Guid.NewGuid(),
                    ProviderName = "Ring",
                    LinkedUtc = DateTime.UtcNow,
                    IsActive = true
                });

            _runner.Setup(r => r.RunAsync(_accountId, It.IsAny<AccountSyncRequest>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, AccountSyncRequest, CancellationToken>((_, request, _) => _ranWith.TrySetResult(request))
                .Returns(Task.CompletedTask);

            // No body takes the legacy overload, which has no request to inspect.
            _runner.Setup(r => r.RunAsync(_accountId, It.IsAny<CancellationToken>()))
                .Callback<Guid, CancellationToken>((_, _) => _ranLegacy.TrySetResult())
                .Returns(Task.CompletedTask);

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            // Every type the account endpoints inject must be a registered service, otherwise route inference treats it as a body parameter.
            _ = builder.Services.AddSingleton(Mock.Of<IUserRepository>());
            _ = builder.Services.AddSingleton(_accounts.Object);
            _ = builder.Services.AddSingleton(Mock.Of<ICaseRepository>());
            _ = builder.Services.AddSingleton(_runner.Object);
            _ = builder.Services.AddSingleton(Mock.Of<IProviderSyncRunRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<VideoForensics.Client.Common.Contracts.IAccountExportService>());
            _ = builder.Services.AddSingleton(_syncState.Object);

            // The account routes require SuperAdmin on the Local tier; the test scheme always signs in such an operator.
            var tier = new Mock<INetworkTierResolver>();
            _ = tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            _ = builder.Services.AddSingleton(tier.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, SuperAdminTestHandler>(TestScheme, _ => { });

            _app = builder.Build();
            _ = _app.UseAuthentication();
            _ = _app.UseAuthorization();
            _app.MapAccountEndpoints();
            await _app.StartAsync();
            _client = _app.GetTestClient();
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

        private const string TestScheme = "SyncNowTest";

        /// <summary>Signs every request in as a SuperAdmin on the Local tier.</summary>
        private sealed class SuperAdminTestHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public SuperAdminTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, OperatorRole.SuperAdmin.ToString()),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, "Local"),
                    new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
                ], TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        [Theory]
        [InlineData("{\"syncType\":\"Bogus\"}")]
        [InlineData("{\"rangeKind\":\"Forever\"}")]
        [InlineData("{\"rangeKind\":\"Days\",\"days\":0}")]
        [InlineData("{\"rangeKind\":\"Days\",\"days\":3651}")]
        [InlineData("{\"rangeKind\":\"Days\"}")]
        [InlineData("{\"rangeKind\":\"Custom\"}")]
        [InlineData("{\"rangeKind\":\"Custom\",\"startUtc\":\"2026-10-16T10:00:00Z\",\"endUtc\":\"2026-10-16T10:00:00Z\"}")]
        [InlineData("{\"rangeKind\":\"Custom\",\"startUtc\":\"2026-10-16T12:00:00Z\",\"endUtc\":\"2026-10-16T10:00:00Z\"}")]
        public async Task SyncNow_InvalidBody_Returns400AndDoesNotRun(string json)
        {
            HttpResponseMessage response = await _client.PostAsync(SyncNowUrl, Json(json));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _runner.Verify(r => r.RunAsync(It.IsAny<Guid>(), It.IsAny<AccountSyncRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SyncNow_NoBody_RunsLegacySync()
        {
            HttpResponseMessage response = await _client.PostAsync(SyncNowUrl, null);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await _ranLegacy.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _runner.Verify(r => r.RunAsync(It.IsAny<Guid>(), It.IsAny<AccountSyncRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SyncNow_EventsDaysBody_PassesDaysRange()
        {
            DateTime before = DateTime.UtcNow;
            _ = await _client.PostAsync(SyncNowUrl, Json("{\"syncType\":\"Events\",\"rangeKind\":\"Days\",\"days\":7}"));
            DateTime after = DateTime.UtcNow;

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AccountSyncType.Events, request.Type);
            Assert.False(request.AllHistory);
            Assert.Null(request.ToUtc);
            Assert.InRange(request.FromUtc!.Value, before.AddDays(-7), after.AddDays(-7));
        }

        [Fact]
        public async Task SyncNow_CustomBody_PassesStartAndEnd()
        {
            _ = await _client.PostAsync(SyncNowUrl, Json(
                "{\"syncType\":\"Snapshots\",\"rangeKind\":\"Custom\",\"startUtc\":\"2026-10-01T00:00:00Z\",\"endUtc\":\"2026-10-02T00:00:00Z\"}"));

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AccountSyncType.Snapshots, request.Type);
            Assert.False(request.AllHistory);
            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), request.FromUtc);
            Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), request.ToUtc);
        }

        [Fact]
        public async Task SyncNow_CustomBodyWithOffset_ConvertsToTheSameUtcInstant()
        {
            _ = await _client.PostAsync(SyncNowUrl, Json(
                "{\"syncType\":\"Events\",\"rangeKind\":\"Custom\",\"startUtc\":\"2026-10-01T02:00:00+02:00\",\"endUtc\":\"2026-10-02T02:00:00+02:00\"}"));

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), request.FromUtc!.Value.ToUniversalTime());
            Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), request.ToUtc!.Value.ToUniversalTime());
        }

        [Fact]
        public async Task SyncNow_AllTypeAllHistory_RunsAllTypeOverFullHistory()
        {
            _ = await _client.PostAsync(SyncNowUrl, Json("{\"syncType\":\"All\",\"rangeKind\":\"All\"}"));

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AccountSyncType.All, request.Type);
            Assert.True(request.AllHistory);
            Assert.Null(request.FromUtc);
            Assert.Null(request.ToUtc);
        }

        [Fact]
        public async Task SyncNow_AllTypeDays_KeepsAllTypeAndDaysRange()
        {
            DateTime before = DateTime.UtcNow;
            _ = await _client.PostAsync(SyncNowUrl, Json("{\"syncType\":\"all\",\"rangeKind\":\"Days\",\"days\":30}"));
            DateTime after = DateTime.UtcNow;

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AccountSyncType.All, request.Type);
            Assert.False(request.AllHistory);
            Assert.InRange(request.FromUtc!.Value, before.AddDays(-30), after.AddDays(-30));
        }

        [Fact]
        public async Task SyncNow_EventsAllHistory_RunsEventsOverFullHistory()
        {
            _ = await _client.PostAsync(SyncNowUrl, Json("{\"syncType\":\"Events\",\"rangeKind\":\"All\"}"));

            AccountSyncRequest request = await _ranWith.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AccountSyncType.Events, request.Type);
            Assert.True(request.AllHistory);
        }

        [Fact]
        public async Task ConfigSyncInterval_Get_ReturnsStoredHours()
        {
            _syncState.Setup(s => s.GetConfigSyncStateAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RingConfigSyncState(48, null));

            ConfigSyncIntervalDto? dto = await _client.GetFromJsonAsync<ConfigSyncIntervalDto>(IntervalUrl, WebJson);

            Assert.Equal(48, dto!.Hours);
        }

        [Fact]
        public async Task ConfigSyncInterval_PutValid_StoresHours()
        {
            _syncState.Setup(s => s.SetConfigIntervalHoursAsync(_accountId, 48, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            HttpResponseMessage response = await _client.PutAsync(IntervalUrl, Json("{\"hours\":48}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _syncState.Verify(s => s.SetConfigIntervalHoursAsync(_accountId, 48, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(721)]
        public async Task ConfigSyncInterval_PutOutOfRange_Returns400AndDoesNotStore(int hours)
        {
            HttpResponseMessage response = await _client.PutAsync(IntervalUrl, Json($"{{\"hours\":{hours}}}"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _syncState.Verify(s => s.SetConfigIntervalHoursAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConfigSyncInterval_PutWithoutRingRow_Returns404()
        {
            _syncState.Setup(s => s.SetConfigIntervalHoursAsync(_accountId, 12, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            HttpResponseMessage response = await _client.PutAsync(IntervalUrl, Json("{\"hours\":12}"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
