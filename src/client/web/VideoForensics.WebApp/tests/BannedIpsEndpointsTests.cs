using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Services;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class BannedIpsEndpointsTests
    {
        private static BannedIpRange CreateBan(string cidr, Guid createdBy, DateTime? expiresAtUtc = null) => new()
        {
            Id = Guid.NewGuid(),
            CidrRange = cidr,
            Reason = "Auto-ban: 3 accounts locked in 60 min",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedByOperatorId = createdBy,
            ExpiresAtUtc = expiresAtUtc
        };

        [Fact]
        public async Task ListBannedIps_ReturnsActiveBansAsDtos()
        {
            // Arrange
            var manualBan = CreateBan("203.0.113.0/24", Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var systemBan = CreateBan("198.51.100.7/32", BannedIpRangeDtoMapping.SystemAutoBanOperatorId, DateTime.UtcNow.AddHours(24));
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BannedIpRange> { manualBan, systemBan });

            // Act
            Ok<IReadOnlyList<BannedIpRangeDto>> result = await BannedIpsEndpoints.ListAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Value!.Count);
            BannedIpRangeDto first = result.Value[0];
            Assert.Equal(manualBan.Id, first.Id);
            Assert.Equal("203.0.113.0/24", first.CidrRange);
            Assert.Equal(manualBan.Reason, first.Reason);
            Assert.Equal(manualBan.CreatedAtUtc, first.CreatedAtUtc);
            Assert.Equal(manualBan.ExpiresAtUtc, first.ExpiresAtUtc);
            Assert.Equal(manualBan.CreatedByOperatorId, first.CreatedByOperatorId);
            Assert.False(first.IsAutoBan);
        }

        [Fact]
        public async Task ListBannedIps_SystemOperatorBan_IsFlaggedAsAutoBan()
        {
            // Arrange
            var systemBan = CreateBan("198.51.100.7/32", BannedIpRangeDtoMapping.SystemAutoBanOperatorId, DateTime.UtcNow.AddHours(24));
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BannedIpRange> { systemBan });

            // Act
            Ok<IReadOnlyList<BannedIpRangeDto>> result = await BannedIpsEndpoints.ListAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.True(result.Value![0].IsAutoBan);
        }

        [Fact]
        public async Task ListBannedIps_ExpiredBanReturnedByRepository_IsExcluded()
        {
            // Arrange: the repository contract promises only active bans, but a stale or mis-mocked
            // repository must not leak an expired ban to the admin list.
            var active = CreateBan("203.0.113.0/24", Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var expired = CreateBan("192.0.2.0/24", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1));
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BannedIpRange> { active, expired });

            // Act
            Ok<IReadOnlyList<BannedIpRangeDto>> result = await BannedIpsEndpoints.ListAsync(repository.Object, CancellationToken.None);

            // Assert
            BannedIpRangeDto only = Assert.Single(result.Value!);
            Assert.Equal("203.0.113.0/24", only.CidrRange);
        }

        [Fact]
        public async Task ListBannedIps_ForwardsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(cts.Token))
                .ReturnsAsync(new List<BannedIpRange>());

            // Act
            _ = await BannedIpsEndpoints.ListAsync(repository.Object, cts.Token);

            // Assert
            repository.Verify(r => r.GetActiveAsync(cts.Token), Times.Once);
        }

        [Fact]
        public void MapBannedIpsEndpoints_RequiresSuperAdminLocal()
        {
            // Arrange: map the routes onto a real endpoint table; a non-admin is rejected by the
            // SuperAdminLocal policy, so the route metadata must carry that policy name.
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(new Mock<IBannedIpRangeRepository>().Object);
            builder.Services.AddSingleton(new Mock<ISecurityAuditLogger>().Object);
            builder.Services.AddSingleton(new Mock<IFailedLoginIpTracker>().Object);
            builder.Services.AddSingleton(new Mock<INetworkTierResolver>().Object);
            WebApplication app = builder.Build();

            // Act
            app.MapBannedIpsEndpoints();

            // Assert
            RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(e => e.RoutePattern.RawText?.TrimEnd('/') == "/api/v1/banned-ips");
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
        }

        [Fact]
        public void MapBannedIpsEndpoints_DeleteRoute_RequiresSuperAdminLocalAndAcceptsDelete()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(new Mock<IBannedIpRangeRepository>().Object);
            builder.Services.AddSingleton(new Mock<ISecurityAuditLogger>().Object);
            builder.Services.AddSingleton(new Mock<IFailedLoginIpTracker>().Object);
            builder.Services.AddSingleton(new Mock<INetworkTierResolver>().Object);
            WebApplication app = builder.Build();

            app.MapBannedIpsEndpoints();

            RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(e => e.RoutePattern.RawText == "/api/v1/banned-ips/{id:guid}");
            IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
            Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            Assert.Contains("DELETE", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        }

        // Endpoint filters are not visible in route metadata, so the step-up requirement is proven through
        // a real TestServer pipeline: real routing, the SuperAdminLocal policy and StepUpEndpointFilter.
        private const string PipelineScheme = "BannedIpsPipelineTest";

        private sealed class PipelineAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public PipelineAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, "SuperAdmin"),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, "Local"),
                    new Claim(VideoForensicsClaimTypes.OperatorId, Guid.NewGuid().ToString())
                ], PipelineScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), PipelineScheme)));
            }
        }

        private static async Task<WebApplication> StartPipelineAsync(
            Mock<IBannedIpRangeRepository> repository,
            bool stepUpValid)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var stepUp = new Mock<IStepUpAuthService>();
            _ = stepUp.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<Guid>())).Returns(stepUpValid);

            _ = builder.Services.AddSingleton(repository.Object);
            _ = builder.Services.AddSingleton(new Mock<ISecurityAuditLogger>().Object);
            _ = builder.Services.AddSingleton(new Mock<IFailedLoginIpTracker>().Object);
            _ = builder.Services.AddSingleton(TierResolver().Object);
            _ = builder.Services.AddSingleton(stepUp.Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(PipelineScheme)
                .AddScheme<AuthenticationSchemeOptions, PipelineAuthHandler>(PipelineScheme, _ => { });

            WebApplication app = builder.Build();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapBannedIpsEndpoints();
            await app.StartAsync();
            return app;
        }

        [Fact]
        public async Task UnbanBannedIp_MissingStepUpToken_Returns403AndLeavesBanInPlace()
        {
            // Arrange
            var id = Guid.NewGuid();
            var repository = new Mock<IBannedIpRangeRepository>();
            await using WebApplication app = await StartPipelineAsync(repository, stepUpValid: true);
            using HttpClient client = app.GetTestClient();

            // Act: no X-StepUp-Token header
            HttpResponseMessage response = await client.DeleteAsync($"/api/v1/banned-ips/{id}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
            repository.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
            repository.Verify(r => r.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UnbanBannedIp_ValidStepUpToken_Returns204AndRemovesBan()
        {
            // Arrange
            var ban = CreateBan("203.0.113.7/32", Guid.NewGuid());
            var repository = RepositoryWith(ban);
            await using WebApplication app = await StartPipelineAsync(repository, stepUpValid: true);
            using HttpClient client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/banned-ips/{ban.Id}");
            request.Headers.Add("X-StepUp-Token", "valid-step-up-token");

            // Act
            HttpResponseMessage response = await client.SendAsync(request);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
            repository.Verify(r => r.RemoveAsync(ban.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        private static DefaultHttpContext OperatorContext(Guid operatorId) => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()) }, "test"))
        };

        private static Mock<INetworkTierResolver> TierResolver(string clientIp = "198.51.100.9")
        {
            var resolver = new Mock<INetworkTierResolver>();
            _ = resolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns(clientIp);
            return resolver;
        }

        private static Mock<IBannedIpRangeRepository> RepositoryWith(params BannedIpRange[] active)
        {
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(active.ToList());
            return repository;
        }

        [Fact]
        public async Task UnbanBannedIp_KnownActiveId_RemovesRangeAndReturns204()
        {
            // Arrange
            var ban = CreateBan("203.0.113.10/32", Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var repository = RepositoryWith(ban);

            // Act
            Results<NoContent, NotFound> result = await BannedIpsEndpoints.UnbanAsync(
                ban.Id, repository.Object, new Mock<ISecurityAuditLogger>().Object, new Mock<IFailedLoginIpTracker>().Object,
                TierResolver().Object, OperatorContext(Guid.NewGuid()), CancellationToken.None);

            // Assert
            Assert.IsType<NoContent>(result.Result);
            repository.Verify(r => r.RemoveAsync(ban.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnbanBannedIp_UnknownId_Returns404AndChangesNothing()
        {
            // Arrange
            var repository = RepositoryWith();
            var tracker = new Mock<IFailedLoginIpTracker>();
            var audit = new Mock<ISecurityAuditLogger>();

            // Act
            Results<NoContent, NotFound> result = await BannedIpsEndpoints.UnbanAsync(
                Guid.NewGuid(), repository.Object, audit.Object, tracker.Object,
                TierResolver().Object, OperatorContext(Guid.NewGuid()), CancellationToken.None);

            // Assert
            Assert.IsType<NotFound>(result.Result);
            repository.Verify(r => r.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            tracker.Verify(t => t.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UnbanBannedIp_Success_WritesUnbanAuditEntryWithActorAndRange()
        {
            // Arrange
            var ban = CreateBan("203.0.113.10/32", Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var repository = RepositoryWith(ban);
            var audit = new Mock<ISecurityAuditLogger>();
            var actor = Guid.NewGuid();

            // Act
            _ = await BannedIpsEndpoints.UnbanAsync(
                ban.Id, repository.Object, audit.Object, new Mock<IFailedLoginIpTracker>().Object,
                TierResolver("198.51.100.9").Object, OperatorContext(actor), CancellationToken.None);

            // Assert: the acting operator goes in the operator column; details name the range and never the creator's GUID.
            audit.Verify(a => a.LogAsync(SecurityAuditEventTypes.BannedIpRangeRemoved, actor, null, "198.51.100.9",
                It.Is<string?>(d => d != null && d.Contains("203.0.113.10/32") && !d.Contains(ban.CreatedByOperatorId.ToString())),
                true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnbanBannedIp_HostBan_ClearsTrackerForBannedAddress()
        {
            // Arrange
            var ban = CreateBan("203.0.113.10/32", Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var repository = RepositoryWith(ban);
            var tracker = new Mock<IFailedLoginIpTracker>();
            tracker.Setup(t => t.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            _ = await BannedIpsEndpoints.UnbanAsync(
                ban.Id, repository.Object, new Mock<ISecurityAuditLogger>().Object, tracker.Object,
                TierResolver().Object, OperatorContext(Guid.NewGuid()), CancellationToken.None);

            // Assert: a single-host ban clears the tracker's counter for that exact address.
            tracker.Verify(t => t.ClearAsync("203.0.113.10", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("2001:db8::25/128", "2001:db8::25")]
        [InlineData("203.0.113.0/24", null)]
        [InlineData("2001:db8::/64", null)]
        public async Task UnbanBannedIp_RangeOrIpv6Host_ClearsTrackerOnlyForSingleHost(string cidr, string? expectedClearedIp)
        {
            // Arrange: a wider prefix covers many addresses, so the tracker must not drop counters for them.
            var ban = CreateBan(cidr, Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
            var repository = RepositoryWith(ban);
            var tracker = new Mock<IFailedLoginIpTracker>();
            tracker.Setup(t => t.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            _ = await BannedIpsEndpoints.UnbanAsync(
                ban.Id, repository.Object, new Mock<ISecurityAuditLogger>().Object, tracker.Object,
                TierResolver().Object, OperatorContext(Guid.NewGuid()), CancellationToken.None);

            // Assert
            if (expectedClearedIp is null)
            {
                tracker.Verify(t => t.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            else
            {
                tracker.Verify(t => t.ClearAsync(expectedClearedIp, It.IsAny<CancellationToken>()), Times.Once);
            }
        }
    }
}
