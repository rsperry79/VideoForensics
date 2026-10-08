using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class OperatorUiModeEndpointTests
    {
        private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId, OperatorRole role = OperatorRole.Admin)
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
        public async Task GetUiMode_UnknownOperator_ReturnsNotFound()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var preferences = new Mock<IOperatorPreferencesRepository>();

            // Act
            var result = await OperatorUiModeEndpointInvoker.GetUiModeAsync(
                operatorId, operators.Object, preferences.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Verify it's a NotFound result
            var notFoundResult = Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task GetUiMode_OperatorExists_NoPreferences_ReturnsDefaults()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test Operator" });

            var preferences = new Mock<IOperatorPreferencesRepository>();
            preferences.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorPreferences?)null);

            // Act
            var result = await OperatorUiModeEndpointInvoker.GetUiModeAsync(
                operatorId, operators.Object, preferences.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Ok<OperatorUiModeDto>>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal("Standard", okResult.Value.Mode);
            Assert.False(okResult.Value.Locked);
        }

        [Fact]
        public async Task GetUiMode_OperatorExists_WithPreferences_ReturnsStoredValues()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test Operator" });

            var storedPrefs = new OperatorPreferences
            {
                OperatorId = operatorId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "Light"
            };

            var preferences = new Mock<IOperatorPreferencesRepository>();
            preferences.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedPrefs);

            // Act
            var result = await OperatorUiModeEndpointInvoker.GetUiModeAsync(
                operatorId, operators.Object, preferences.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Ok<OperatorUiModeDto>>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal("Simple", okResult.Value.Mode);
            Assert.True(okResult.Value.Locked);
        }

        [Fact]
        public async Task GetUiMode_NoAuditLogging()
        {
            // Arrange - GET should never log audit events
            var operatorId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test" });

            var preferences = new Mock<IOperatorPreferencesRepository>();
            preferences.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorPreferences?)null);

            // Act
            var result = await OperatorUiModeEndpointInvoker.GetUiModeAsync(
                operatorId, operators.Object, preferences.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Ok<OperatorUiModeDto>>(result);
            // Verify no audit logging occurs for GET (audit logger is not even injected into GetUiModeAsync)
        }

        [Fact]
        public async Task SetUiMode_UnknownOperator_ReturnsNotFound()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var request = new SetOperatorUiModeRequest("Simple", true);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            var preferences = new Mock<IOperatorPreferencesRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            var result = await OperatorUiModeEndpointInvoker.SetUiModeAsync(
                operatorId, request, operators.Object, preferences.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var notFoundResult = Assert.IsType<NotFound>(result);
            preferences.Verify(r => r.SetUiModeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetUiMode_InvalidMode_ReturnsBadRequest()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var request = new SetOperatorUiModeRequest("InvalidMode", false);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test" });

            var preferences = new Mock<IOperatorPreferencesRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            var result = await OperatorUiModeEndpointInvoker.SetUiModeAsync(
                operatorId, request, operators.Object, preferences.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var badRequestResult = Assert.IsType<BadRequest>(result);
            preferences.Verify(r => r.SetUiModeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            auditLog.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetUiMode_ValidRequest_PersistsAndReturns()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var request = new SetOperatorUiModeRequest("Simple", true);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test" });

            var savedPrefs = new OperatorPreferences
            {
                OperatorId = operatorId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "System"
            };

            var preferences = new Mock<IOperatorPreferencesRepository>();
            preferences.Setup(r => r.SetUiModeAsync(operatorId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPrefs);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            var result = await OperatorUiModeEndpointInvoker.SetUiModeAsync(
                operatorId, request, operators.Object, preferences.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<Ok<OperatorUiModeDto>>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal("Simple", okResult.Value.Mode);
            Assert.True(okResult.Value.Locked);

            preferences.Verify(r => r.SetUiModeAsync(operatorId, "Simple", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetUiMode_ValidRequest_LogsAuditEvent()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var targetUsername = "testoperator";
            var request = new SetOperatorUiModeRequest("Simple", true);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator { Id = operatorId, DisplayName = "Test Operator", Username = targetUsername });

            var savedPrefs = new OperatorPreferences
            {
                OperatorId = operatorId,
                UiMode = "Simple",
                UiModeLocked = true,
                ThemeMode = "System"
            };

            var preferences = new Mock<IOperatorPreferencesRepository>();
            preferences.Setup(r => r.SetUiModeAsync(operatorId, "Simple", true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPrefs);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var tierResolver = MockNetworkTierResolver();
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            await OperatorUiModeEndpointInvoker.SetUiModeAsync(
                operatorId, request, operators.Object, preferences.Object, auditLog.Object, tierResolver.Object, context, CancellationToken.None);

            // Assert - verify audit event includes target operator username and mode/locked values
            auditLog.Verify(a => a.LogAsync(
                SecurityAuditEventTypes.OperatorUiModeChanged,
                adminId, null, "127.0.0.1",
                It.Is<string>(details => details.Contains($"target={targetUsername}") && details.Contains("mode=Simple") && details.Contains("locked=True")),
                false, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>
    /// Helper class to invoke the private GetUiModeAsync and SetUiModeAsync methods in DeviceManagementEndpoints for testing.
    /// </summary>
    internal static class OperatorUiModeEndpointInvoker
    {
        public static async Task<object> GetUiModeAsync(
            Guid operatorId,
            IOperatorRepository operators,
            IOperatorPreferencesRepository preferences,
            CancellationToken ct)
        {
            var method = typeof(VideoForensics.WebApp.Api.DeviceManagementEndpoints)
                .GetMethod("GetUiModeAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(Guid), typeof(IOperatorRepository), typeof(IOperatorPreferencesRepository),
                     typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find GetUiModeAsync method");

            var result = method.Invoke(null, [operatorId, operators, preferences, ct]);
            return await (dynamic)result;
        }

        public static async Task<object> SetUiModeAsync(
            Guid operatorId,
            SetOperatorUiModeRequest request,
            IOperatorRepository operators,
            IOperatorPreferencesRepository preferences,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(VideoForensics.WebApp.Api.DeviceManagementEndpoints)
                .GetMethod("SetUiModeAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(Guid), typeof(SetOperatorUiModeRequest), typeof(IOperatorRepository),
                     typeof(IOperatorPreferencesRepository), typeof(ISecurityAuditLogger),
                     typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find SetUiModeAsync method");

            var result = method.Invoke(null, [operatorId, request, operators, preferences, auditLog, tierResolver, context, ct]);
            return await (dynamic)result;
        }
    }
}
