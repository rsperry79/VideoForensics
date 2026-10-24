using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Moq;

using System.Reflection;
using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class TwoFactorPolicyEndpointsTests
    {
        private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId, OperatorRole role = OperatorRole.SuperAdmin)
        {
            var context = new DefaultHttpContext();
            Claim[] claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim(VideoForensicsClaimTypes.NetworkTier, NetworkTier.Local.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            return context;
        }

        private static TwoFactorRoleRequirement CreateRoleRequirement(OperatorRole role, bool requireTwoFactor = true)
        {
            return new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = role,
                RequireTwoFactor = requireTwoFactor,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };
        }

        private static Mock<INetworkTierResolver> MockNetworkTierResolver()
        {
            var mock = new Mock<INetworkTierResolver>();
            _ = mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        [Fact]
        public async Task GetAllRoles_ReturnsAllRoleRequirements()
        {
            // Arrange
            var requirements = new List<TwoFactorRoleRequirement>
            {
                CreateRoleRequirement(OperatorRole.ReadOnly, true),
                CreateRoleRequirement(OperatorRole.Review, true),
                CreateRoleRequirement(OperatorRole.Admin, true),
                CreateRoleRequirement(OperatorRole.SuperAdmin, true)
            };

            var repository = new Mock<ITwoFactorRoleRequirementRepository>();
            _ = repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(requirements);

            // Act
            object result = await TwoFactorPolicyEndpointsInvoker.GetAllRolesAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateRoleRequirement_UpdatesSettingAndLogsAudit()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            TwoFactorRoleRequirement existingRequirement = CreateRoleRequirement(OperatorRole.ReadOnly, true);
            var request = new UpdateTwoFactorRoleRequirementRequest(false);

            var repository = new Mock<ITwoFactorRoleRequirementRepository>();
            _ = repository.Setup(r => r.UpsertAsync(It.IsAny<TwoFactorRoleRequirement>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            object result = await TwoFactorPolicyEndpointsInvoker.UpdateRoleRequirementAsync(
                OperatorRole.ReadOnly, request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            repository.Verify(r => r.UpsertAsync(It.IsAny<TwoFactorRoleRequirement>(), It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.TwoFactorPolicyUpdated,
                operatorId, null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOperatorOverride_UpdatesOverrideAndLogsAudit()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var actingOperatorId = Guid.NewGuid();
            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Test User",
                Username = "testuser",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Role = OperatorRole.ReadOnly,
                SecurityStamp = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                TwoFactorRequirementOverride = TwoFactorRequirementOverride.Inherit
            };

            var request = new UpdateOperatorTwoFactorOverrideRequest(TwoFactorRequirementOverride.Required);

            var operatorRepository = new Mock<IOperatorRepository>();
            _ = operatorRepository.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            _ = operatorRepository.Setup(r => r.SetTwoFactorRequirementOverrideAsync(operatorId, It.IsAny<TwoFactorRequirementOverride>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(actingOperatorId);

            // Act
            object result = await TwoFactorPolicyEndpointsInvoker.UpdateOperatorOverrideAsync(
                operatorId, request, operatorRepository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            operatorRepository.Verify(r => r.SetTwoFactorRequirementOverrideAsync(operatorId, TwoFactorRequirementOverride.Required, It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.TwoFactorPolicyUpdated,
                actingOperatorId, null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOperatorOverride_WithNonexistentOperator_ReturnsNotFound()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var actingOperatorId = Guid.NewGuid();
            var request = new UpdateOperatorTwoFactorOverrideRequest(TwoFactorRequirementOverride.Required);

            var operatorRepository = new Mock<IOperatorRepository>();
            _ = operatorRepository.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var auditLog = new Mock<ISecurityAuditLogger>();
            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(actingOperatorId);

            // Act
            object result = await TwoFactorPolicyEndpointsInvoker.UpdateOperatorOverrideAsync(
                operatorId, request, operatorRepository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Should return 404
            operatorRepository.Verify(r => r.SetTwoFactorRequirementOverrideAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorRequirementOverride>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void TwoFactorPolicyEndpoints_MappedRoutes_RequireSuperAdminLocalOrPasskeyPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint[] endpoints = app.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.StartsWith("/api/v1/two-factor-policy", StringComparison.Ordinal) == true)
                .ToArray();

            Assert.NotEmpty(endpoints);
            foreach (RouteEndpoint endpoint in endpoints)
            {
                IAuthorizeData[] authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
                Assert.Contains(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocalOrPasskey);
                Assert.DoesNotContain(authorize, a => a.Policy == VideoForensicsPolicies.SuperAdminLocal);
            }
        }

        private static IEndpointRouteBuilder BuildAndMap()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });
            _ = builder.Services.AddScoped(_ => Mock.Of<ITwoFactorRoleRequirementRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IOperatorRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<ISecurityAuditLogger>());
            _ = builder.Services.AddScoped(_ => Mock.Of<INetworkTierResolver>());

            WebApplication app = builder.Build();
            app.MapTwoFactorPolicyEndpoints();
            return app;
        }
    }

    /// <summary>
    /// Helper class to invoke private methods in TwoFactorPolicyEndpoints for testing.
    /// </summary>
    internal static class TwoFactorPolicyEndpointsInvoker
    {
        public static async Task<object> GetAllRolesAsync(ITwoFactorRoleRequirementRepository repository, CancellationToken ct)
        {
            MethodInfo? method = typeof(TwoFactorPolicyEndpoints)
                .GetMethod("GetAllRolesAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ITwoFactorRoleRequirementRepository), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find GetAllRolesAsync method");
            }

            object? result = method.Invoke(null, [repository, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> UpdateRoleRequirementAsync(
            OperatorRole role,
            UpdateTwoFactorRoleRequirementRequest request,
            ITwoFactorRoleRequirementRepository repository,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(TwoFactorPolicyEndpoints)
                .GetMethod("UpdateRoleRequirementAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(OperatorRole), typeof(UpdateTwoFactorRoleRequirementRequest), typeof(ITwoFactorRoleRequirementRepository),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find UpdateRoleRequirementAsync method");
            }

            object? result = method.Invoke(null, [role, request, repository, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> UpdateOperatorOverrideAsync(
            Guid operatorId,
            UpdateOperatorTwoFactorOverrideRequest request,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(TwoFactorPolicyEndpoints)
                .GetMethod("UpdateOperatorOverrideAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(Guid), typeof(UpdateOperatorTwoFactorOverrideRequest), typeof(IOperatorRepository),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find UpdateOperatorOverrideAsync method");
            }

            object? result = method.Invoke(null, [operatorId, request, operators, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
