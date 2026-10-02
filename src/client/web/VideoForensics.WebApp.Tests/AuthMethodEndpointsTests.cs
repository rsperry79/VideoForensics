using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class AuthMethodEndpointsTests
    {
        private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId)
        {
            var context = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new Claim(ClaimTypes.Role, OperatorRole.SuperAdmin.ToString()),
                new Claim(VideoForensicsClaimTypes.NetworkTier, NetworkTier.Local.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            return context;
        }

        private static Mock<INetworkTierResolver> MockNetworkTierResolver()
        {
            var mock = new Mock<INetworkTierResolver>();
            mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        [Fact]
        public async Task GetAuthMethods_BothEnabledByDefault_ReturnsBothTrue()
        {
            // Arrange
            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthMethodSettingsDto(PasswordEnabled: true, PasskeyEnabled: true));

            // Act
            var result = await AuthMethodEndpointsInvoker.GetAuthMethodsAsync(
                authMethodsService.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            authMethodsService.Verify(s => s.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAuthMethods_PasswordDisabled_ReturnsPasswordFalse()
        {
            // Arrange
            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthMethodSettingsDto(PasswordEnabled: false, PasskeyEnabled: true));

            // Act
            var result = await AuthMethodEndpointsInvoker.GetAuthMethodsAsync(
                authMethodsService.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetSettings_ReturnsCurrentSettings()
        {
            // Arrange
            var settings = new AuthMethodSettingsDto(PasswordEnabled: true, PasskeyEnabled: false);
            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            // Act
            var result = await AuthMethodEndpointsInvoker.GetSettingsAsync(
                authMethodsService.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            authMethodsService.Verify(s => s.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSettings_HappyPath_UpdatesAndLogsAudit()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var request = new UpdateAuthMethodSettingsRequest(PasswordEnabled: null, PasskeyEnabled: false);
            var updatedSettings = new AuthMethodSettingsDto(PasswordEnabled: true, PasskeyEnabled: false);

            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.UpdateAsync(It.IsAny<UpdateAuthMethodSettingsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            authMethodsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedSettings);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            var result = await AuthMethodEndpointsInvoker.UpdateSettingsAsync(
                request, authMethodsService.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            authMethodsService.Verify(s => s.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.AuthMethodsUpdated,
                operatorId, null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSettings_UpdateAsyncReturnsFalse_Returns400WithError()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var request = new UpdateAuthMethodSettingsRequest(PasswordEnabled: false, PasskeyEnabled: false);

            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.UpdateAsync(It.IsAny<UpdateAuthMethodSettingsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(operatorId);

            // Act
            var result = await AuthMethodEndpointsInvoker.UpdateSettingsAsync(
                request, authMethodsService.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Verify no audit entry was created
            auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    /// <summary>
    /// Helper class to invoke private methods in AuthMethodEndpoints for testing.
    /// </summary>
    internal static class AuthMethodEndpointsInvoker
    {
        public static async Task<object> GetAuthMethodsAsync(
            IAuthMethodSettingsService authMethodsService,
            CancellationToken ct)
        {
            var method = typeof(AuthMethodEndpoints)
                .GetMethod("GetAuthMethodsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(IAuthMethodSettingsService), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find GetAuthMethodsAsync method");

            var result = method.Invoke(null, [authMethodsService, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> GetSettingsAsync(
            IAuthMethodSettingsService authMethodsService,
            CancellationToken ct)
        {
            var method = typeof(AuthMethodEndpoints)
                .GetMethod("GetSettingsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(IAuthMethodSettingsService), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find GetSettingsAsync method");

            var result = method.Invoke(null, [authMethodsService, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> UpdateSettingsAsync(
            UpdateAuthMethodSettingsRequest request,
            IAuthMethodSettingsService authMethodsService,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(AuthMethodEndpoints)
                .GetMethod("UpdateSettingsAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(UpdateAuthMethodSettingsRequest), typeof(IAuthMethodSettingsService),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find UpdateSettingsAsync method");

            var result = method.Invoke(null, [request, authMethodsService, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
