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
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LockoutPolicyEndpointsTests
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

        private static LockoutPolicySettings CreateDefaultSettings()
        {
            return new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                BlockedCountryCodes = null,
                FailClosedOnLookupError = false,
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
        public async Task GetLockoutPolicy_ReturnsCurrentSettings()
        {
            // Arrange
            LockoutPolicySettings settings = CreateDefaultSettings();
            var repository = new Mock<ILockoutPolicySettingsRepository>();
            _ = repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            HttpContext context = CreateAuthenticatedHttpContext(Guid.NewGuid());

            // Act
            object result = await LockoutPolicyEndpointsInvoker.GetAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            repository.Verify(r => r.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLockoutPolicy_UpdatesSettingsAndLogsAudit()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            LockoutPolicySettings settings = CreateDefaultSettings();
            var request = new UpdateLockoutPolicySettingsRequest(
                MaxFailedAttempts: 10,
                LockoutDurationMinutes: 30,
                BlockedCountryCodes: "KP,IR",
                FailClosedOnLookupError: true
            );

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            _ = repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);
            _ = repository.Setup(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            object result = await LockoutPolicyEndpointsInvoker.UpdateAsync(
                request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            repository.Verify(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.LockoutPolicyUpdated,
                operatorId, null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLockoutPolicy_UpdatesSettingsWithCurrentOperatorId()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            LockoutPolicySettings settings = CreateDefaultSettings();
            var request = new UpdateLockoutPolicySettingsRequest(10, 30, null, false);

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            LockoutPolicySettings? capturedSettings = null;
            _ = repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);
            _ = repository.Setup(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()))
                .Callback<LockoutPolicySettings, CancellationToken>((s, ct) => capturedSettings = s)
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            _ = await LockoutPolicyEndpointsInvoker.UpdateAsync(
                request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedSettings);
            Assert.Equal(operatorId, capturedSettings.UpdatedByOperatorId);
            Assert.Equal(10, capturedSettings.MaxFailedAttempts);
        }

        [Theory]
        [InlineData(0, 60, 1440)]
        [InlineData(-1, 60, 1440)]
        [InlineData(3, 0, 1440)]
        [InlineData(3, -5, 1440)]
        [InlineData(3, 60, 0)]
        [InlineData(3, 60, -1)]
        public async Task UpdateLockoutPolicy_NonPositiveAutoBanValue_ReturnsBadRequest(
            int distinctAccountThreshold, int windowMinutes, int durationMinutes)
        {
            // Arrange
            var request = new UpdateLockoutPolicySettingsRequest(
                MaxFailedAttempts: 5,
                LockoutDurationMinutes: 15,
                BlockedCountryCodes: null,
                FailClosedOnLookupError: false,
                AutoBanEnabled: true,
                AutoBanDistinctAccountThreshold: distinctAccountThreshold,
                AutoBanWindowMinutes: windowMinutes,
                AutoBanDurationMinutes: durationMinutes);

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            _ = repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateDefaultSettings());

            var auditLog = new Mock<ISecurityAuditLogger>();
            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(Guid.NewGuid());

            // Act
            object result = await LockoutPolicyEndpointsInvoker.UpdateAsync(
                request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            IStatusCodeHttpResult statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
            repository.Verify(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateLockoutPolicy_ValidAutoBanValues_PersistsAllAutoBanFields()
        {
            // Arrange
            var request = new UpdateLockoutPolicySettingsRequest(
                MaxFailedAttempts: 5,
                LockoutDurationMinutes: 15,
                BlockedCountryCodes: null,
                FailClosedOnLookupError: false,
                AutoBanEnabled: true,
                AutoBanDistinctAccountThreshold: 4,
                AutoBanWindowMinutes: 90,
                AutoBanDurationMinutes: 2880);

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            LockoutPolicySettings? capturedSettings = null;
            _ = repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateDefaultSettings());
            _ = repository.Setup(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()))
                .Callback<LockoutPolicySettings, CancellationToken>((s, ct) => capturedSettings = s)
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            _ = auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver();
            HttpContext context = CreateAuthenticatedHttpContext(Guid.NewGuid());

            // Act
            object result = await LockoutPolicyEndpointsInvoker.UpdateAsync(
                request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedSettings);
            Assert.True(capturedSettings.AutoBanEnabled);
            Assert.Equal(4, capturedSettings.AutoBanDistinctAccountThreshold);
            Assert.Equal(90, capturedSettings.AutoBanWindowMinutes);
            Assert.Equal(2880, capturedSettings.AutoBanDurationMinutes);
            IStatusCodeHttpResult okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        }

        [Fact]
        public void LockoutPolicyEndpoints_MappedRoutes_RequireSuperAdminLocalOrPasskeyPolicy()
        {
            IEndpointRouteBuilder app = BuildAndMap();

            RouteEndpoint[] endpoints = app.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.StartsWith("/api/v1/lockout-policy", StringComparison.Ordinal) == true)
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
            _ = builder.Services.AddScoped(_ => Mock.Of<ILockoutPolicySettingsRepository>());
            _ = builder.Services.AddScoped(_ => Mock.Of<ISecurityAuditLogger>());
            _ = builder.Services.AddScoped(_ => Mock.Of<INetworkTierResolver>());

            WebApplication app = builder.Build();
            app.MapLockoutPolicyEndpoints();
            return app;
        }
    }

    /// <summary>
    /// Helper class to invoke private methods in LockoutPolicyEndpoints for testing.
    /// </summary>
    internal static class LockoutPolicyEndpointsInvoker
    {
        public static async Task<object> GetAsync(ILockoutPolicySettingsRepository repository, CancellationToken ct)
        {
            MethodInfo? method = typeof(LockoutPolicyEndpoints)
                .GetMethod("GetAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ILockoutPolicySettingsRepository), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find GetAsync method");
            }

            object? result = method.Invoke(null, [repository, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> UpdateAsync(
            UpdateLockoutPolicySettingsRequest request,
            ILockoutPolicySettingsRepository repository,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(LockoutPolicyEndpoints)
                .GetMethod("UpdateAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(UpdateLockoutPolicySettingsRequest), typeof(ILockoutPolicySettingsRepository),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find UpdateAsync method");
            }

            object? result = method.Invoke(null, [request, repository, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
