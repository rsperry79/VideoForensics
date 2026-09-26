using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using VideoForensics.Api.Contracts;
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
            var claims = new[]
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
            mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        [Fact]
        public async Task GetLockoutPolicy_ReturnsCurrentSettings()
        {
            // Arrange
            var settings = CreateDefaultSettings();
            var repository = new Mock<ILockoutPolicySettingsRepository>();
            repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            var context = CreateAuthenticatedHttpContext(Guid.NewGuid());

            // Act
            var result = await LockoutPolicyEndpointsInvoker.GetAsync(repository.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            repository.Verify(r => r.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLockoutPolicy_UpdatesSettingsAndLogsAudit()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var settings = CreateDefaultSettings();
            var request = new UpdateLockoutPolicySettingsRequest(
                MaxFailedAttempts: 10,
                LockoutDurationMinutes: 30,
                BlockedCountryCodes: "KP,IR",
                FailClosedOnLookupError: true
            );

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);
            repository.Setup(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            var result = await LockoutPolicyEndpointsInvoker.UpdateAsync(
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
            var settings = CreateDefaultSettings();
            var request = new UpdateLockoutPolicySettingsRequest(10, 30, null, false);

            var repository = new Mock<ILockoutPolicySettingsRepository>();
            LockoutPolicySettings? capturedSettings = null;
            repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);
            repository.Setup(r => r.UpsertAsync(It.IsAny<LockoutPolicySettings>(), It.IsAny<CancellationToken>()))
                .Callback<LockoutPolicySettings, CancellationToken>((s, ct) => capturedSettings = s)
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            await LockoutPolicyEndpointsInvoker.UpdateAsync(
                request, repository.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedSettings);
            Assert.Equal(operatorId, capturedSettings.UpdatedByOperatorId);
            Assert.Equal(10, capturedSettings.MaxFailedAttempts);
        }
    }

    /// <summary>
    /// Helper class to invoke private methods in LockoutPolicyEndpoints for testing.
    /// </summary>
    internal static class LockoutPolicyEndpointsInvoker
    {
        public static async Task<object> GetAsync(ILockoutPolicySettingsRepository repository, CancellationToken ct)
        {
            var method = typeof(LockoutPolicyEndpoints)
                .GetMethod("GetAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ILockoutPolicySettingsRepository), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find GetAsync method");

            var result = method.Invoke(null, [repository, ct]);
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
            var method = typeof(LockoutPolicyEndpoints)
                .GetMethod("UpdateAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(UpdateLockoutPolicySettingsRequest), typeof(ILockoutPolicySettingsRepository),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find UpdateAsync method");

            var result = method.Invoke(null, [request, repository, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
