using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class PairingEndpointsTests
    {
        private static HttpContext CreateHttpContext(NetworkTier tier = NetworkTier.Network)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = tier == NetworkTier.Local
                ? System.Net.IPAddress.Loopback
                : System.Net.IPAddress.Parse("192.168.1.1");
            return context;
        }

        [Fact]
        public async Task CheckPasskeyEnabledAsync_PasskeyDisabledNonSetup_Returns403Forbidden()
        {
            // Arrange
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.IsEmptyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);  // Not setup phase

            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.IsEnabledAsync("passkey", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("192.168.1.1");

            var context = CreateHttpContext(NetworkTier.Network);

            // Act
            var result = await PairingEndpoints.CheckPasskeyEnabledAsync(
                operators.Object, authMethodsService.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusCodeResult = Assert.IsAssignableFrom<IResult>(result);
            // Verify the response is a 403 by checking the audit log was called with AuthFailure
            auditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                It.IsAny<string>(), "Passkey sign-in is disabled", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckPasskeyEnabledAsync_PasskeyDisabledSetupPhase_ReturnsNull()
        {
            // Arrange - setup phase (operators table empty)
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.IsEmptyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);  // Setup phase - should bypass the disabled check

            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.IsEnabledAsync("passkey", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var context = CreateHttpContext(NetworkTier.Local);

            // Act
            var result = await PairingEndpoints.CheckPasskeyEnabledAsync(
                operators.Object, authMethodsService.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.Null(result);  // Should allow (return null) during setup phase
            auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckPasskeyEnabledAsync_PasskeyEnabled_ReturnsNullAndNoAudit()
        {
            // Arrange
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.IsEmptyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);  // Not setup phase

            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.IsEnabledAsync("passkey", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);  // Passkey is enabled

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("192.168.1.1");

            var context = CreateHttpContext(NetworkTier.Network);

            // Act
            var result = await PairingEndpoints.CheckPasskeyEnabledAsync(
                operators.Object, authMethodsService.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.Null(result);  // Should allow (return null)
            auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
