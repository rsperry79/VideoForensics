using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
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
    }
}
