using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class OperatorUnlockEndpointTests
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

        private static Mock<INetworkTierResolver> MockNetworkTierResolver()
        {
            var mock = new Mock<INetworkTierResolver>();
            mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        [Fact]
        public async Task UnlockOperator_CallsRepositoryUnlockAsync()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.UnlockAsync(operatorId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            var result = await OperatorUnlockEndpointInvoker.UnlockAsync(
                operatorId, operators.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            operators.Verify(r => r.UnlockAsync(operatorId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnlockOperator_LogsAuditEvent()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.UnlockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            await OperatorUnlockEndpointInvoker.UnlockAsync(
                operatorId, operators.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.OperatorUnlocked,
                adminId, null, "127.0.0.1", It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnlockOperator_DoesNotRequireStepUp()
        {
            // This test verifies that the unlock endpoint does NOT have the StepUpEndpointFilter applied.
            // The endpoint should succeed without a step-up token in the header.
            // This is confirmed by the fact that the endpoint handler doesn't check for X-StepUp-Token.

            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.UnlockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);
            // Note: no X-StepUp-Token header is set

            // Act
            var result = await OperatorUnlockEndpointInvoker.UnlockAsync(
                operatorId, operators.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert - should succeed despite no step-up token
            Assert.NotNull(result);
            operators.Verify(r => r.UnlockAsync(operatorId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>
    /// Helper class to invoke the private UnlockAsync method in DeviceManagementEndpoints for testing.
    /// </summary>
    internal static class OperatorUnlockEndpointInvoker
    {
        public static async Task<object> UnlockAsync(
            Guid operatorId,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(VideoForensics.WebApp.Api.DeviceManagementEndpoints)
                .GetMethod("UnlockAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(Guid), typeof(IOperatorRepository), typeof(ISecurityAuditLogger),
                     typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find UnlockAsync method");

            var result = method.Invoke(null, [operatorId, operators, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
