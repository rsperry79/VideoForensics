using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Hosting.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Pipeline tests for retiring provider accounts, listing removed ones, and queueing/polling account exports.
    /// Requests go through an in-memory TestServer so routing, step-up filtering and body binding run for real.
    /// </summary>
    public class AccountEndpointsRetireExportTests : IAsyncLifetime
    {
        private const string TestScheme = "RetireExportTest";
        private const string StepUpToken = "step-up-ok";

        private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IAccountExportService> _exports = new();

        private WebApplication _app = null!;
        private HttpClient _client = null!;

        public async ValueTask InitializeAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            _ = builder.Services.AddSingleton(Mock.Of<IUserRepository>());
            _ = builder.Services.AddSingleton(_accounts.Object);
            _ = builder.Services.AddSingleton(_exports.Object);
            _ = builder.Services.AddSingleton(Mock.Of<ICaseRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<IAccountSyncRunner>());
            _ = builder.Services.AddSingleton(Mock.Of<IRingAccountSyncState>());
            _ = builder.Services.AddSingleton(Mock.Of<IProviderSyncRunRepository>());
            _ = builder.Services.AddScoped(_ => ProviderAccountEditPolicyStub.Admin());

            var stepUp = new Mock<IStepUpAuthService>();
            _ = stepUp.Setup(s => s.Validate(StepUpToken, It.IsAny<Guid>())).Returns(true);
            _ = builder.Services.AddSingleton(stepUp.Object);

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

        private ProviderAccount CreateAccount(bool active, DateTime? removedUtc = null)
        {
            return new()
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow.AddDays(-10),
                IsActive = active,
                RemovedUtc = removedUtc
            };
        }

        private static HttpRequestMessage StepUpPost(string url, object? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-StepUp-Token", StepUpToken);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: WebJson);
            }

            return request;
        }

        [Fact]
        public async Task AccountEndpoints_RetireProviderAccount_ReturnsUpdatedAccountWithRemovedUtc()
        {
            DateTime removed = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
            _ = _accounts.SetupSequence(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount(true))
                .ReturnsAsync(CreateAccount(false, removed));
            _ = _accounts.Setup(a => a.RetireAsync(_accountId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>())).Returns(Task.CompletedTask);

            HttpResponseMessage response = await _client.SendAsync(StepUpPost($"/api/v1/accounts/provider-accounts/{_accountId}/retire"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ProviderAccountDto? dto = await response.Content.ReadFromJsonAsync<ProviderAccountDto>(WebJson);
            Assert.NotNull(dto);
            Assert.Equal(removed, dto!.RemovedUtc);
            _accounts.Verify(a => a.RetireAsync(_accountId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task AccountEndpoints_RetireProviderAccount_UnknownAccount_Returns404()
        {
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);

            HttpResponseMessage response = await _client.SendAsync(StepUpPost($"/api/v1/accounts/provider-accounts/{_accountId}/retire"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            _accounts.Verify(a => a.RetireAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountEndpoints_ListRemovedAccounts_ReturnsOnlyRemoved()
        {
            DateTime removed = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            _ = _accounts.Setup(a => a.ListRemovedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([CreateAccount(false, removed)]);

            HttpResponseMessage response = await _client.GetAsync("/api/v1/accounts/provider-accounts/removed");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<ProviderAccountDto>? list = await response.Content.ReadFromJsonAsync<List<ProviderAccountDto>>(WebJson);
            ProviderAccountDto dto = Assert.Single(list!);
            Assert.Equal(removed, dto.RemovedUtc);
            _accounts.Verify(a => a.ListRemovedAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AccountEndpoints_QueueExportAndPurge_IsRejected_AndNothingIsRetiredOrQueued()
        {
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateAccount(true));

            HttpResponseMessage response = await _client.SendAsync(StepUpPost($"/api/v1/accounts/provider-accounts/{_accountId}/exports", new { purgeAfterExport = true }));

            // Purge is disabled, so the request is refused before the account is retired or any export is queued.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _accounts.Verify(a => a.RetireAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
            _exports.Verify(e => e.QueueExportAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountEndpoints_QueueExportAndPurge_ReturnsAcceptedWithJobId()
        {
            var jobId = Guid.NewGuid();
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateAccount(false, DateTime.UtcNow));
            _ = _exports.Setup(e => e.QueueExportAsync(_accountId, false, It.IsAny<CancellationToken>(), It.IsAny<string?>())).ReturnsAsync(jobId);
            _ = _exports.Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExportJobStatus(jobId, _accountId, ExportJobState.Queued, null, null, false, DateTime.UtcNow, null));

            HttpResponseMessage response = await _client.SendAsync(StepUpPost($"/api/v1/accounts/provider-accounts/{_accountId}/exports", new { purgeAfterExport = false }));

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            ExportJobDto? dto = await response.Content.ReadFromJsonAsync<ExportJobDto>(WebJson);
            Assert.Equal(jobId, dto!.JobId);
            Assert.Equal("Queued", dto.State);
            _accounts.Verify(a => a.RetireAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountEndpoints_GetExportStatus_UnknownJob_Returns404()
        {
            var jobId = Guid.NewGuid();
            _ = _exports.Setup(e => e.GetStatusAsync(jobId, It.IsAny<CancellationToken>())).ReturnsAsync((ExportJobStatus?)null);

            HttpResponseMessage response = await _client.GetAsync($"/api/v1/accounts/exports/{jobId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AccountEndpoints_GetExportStatus_ForAccount_ReturnsLatestJob()
        {
            var jobId = Guid.NewGuid();
            _ = _exports.Setup(e => e.GetLatestStatusForAccountAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExportJobStatus(jobId, _accountId, ExportJobState.Running, null, null, true, DateTime.UtcNow, null));

            HttpResponseMessage response = await _client.GetAsync($"/api/v1/accounts/provider-accounts/{_accountId}/export-status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ExportJobDto? dto = await response.Content.ReadFromJsonAsync<ExportJobDto>(WebJson);
            Assert.Equal(jobId, dto!.JobId);
            Assert.Equal("Running", dto.State);
        }

        [Fact]
        public async Task AccountEndpoints_GetExportStatus_ForAccount_NoJob_Returns404()
        {
            _ = _exports.Setup(e => e.GetLatestStatusForAccountAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ExportJobStatus?)null);

            HttpResponseMessage response = await _client.GetAsync($"/api/v1/accounts/provider-accounts/{_accountId}/export-status");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AccountEndpoints_UpdateRemovedAccount_ToActive_Returns409AndDoesNotUpdate()
        {
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount(false, DateTime.UtcNow));

            HttpResponseMessage response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/api/v1/accounts/provider-accounts/{_accountId}")
            {
                Content = JsonContent.Create(new { isActive = true }),
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            _accounts.Verify(a => a.UpdateAsync(It.IsAny<ProviderAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AccountEndpoints_SyncNow_OnRemovedAccountThatStillFlagsActive_Returns409()
        {
            // A removed account can still carry IsActive=true from before retirement; sync must check removal too.
            _ = _accounts.Setup(a => a.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAccount(true, DateTime.UtcNow));

            HttpResponseMessage response = await _client.SendAsync(StepUpPost($"/api/v1/accounts/provider-accounts/{_accountId}/sync-now"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}
