using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

using Moq;

using System.Net;
using System.Reflection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>Tests for ISecurityAuditService integration in OperatorAuthEndpoints (Phase 0.5).</summary>
    public class OperatorAuthEndpoints_SecurityAuditIntegrationTests
    {
        private static readonly PasswordHasher<Operator> PasswordHasher = new();
        private const string TestPassword = "TestPassword123!";

        [Fact]
        public async Task LoginPasswordAsync_SuccessfulLogin_RecordsAuditEvent()
        {
            // Arrange - create operator with valid password
            Operator op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            _ = operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _ = operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            _ = credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            _ = twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            Mock<ISessionTokenService> sessionTokens = MockSessionTokenService();
            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            Mock<ILockoutPolicySettingsRepository> lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            (Mock<IBannedIpMatchService>? bannedIpService, Mock<IThreatIntelBlocklistService>? threatIntelService, Mock<IGeoIpLookupService>? geoIpService) = CreateDefaultGeoAndThreatMocks();

            HttpContext context = CreateHttpContext(NetworkTier.Network);
            LoginPasswordRequest request = CreateLoginRequest(op.Username, TestPassword);

            // Act
            IResult result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordLoginAttemptAsync(op.Id, "127.0.0.1", true, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginPasswordAsync_FailedPassword_RecordsFailureEvent()
        {
            // Arrange - operator exists but password is wrong
            Operator op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            _ = operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            _ = credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            Mock<ISessionTokenService> sessionTokens = MockSessionTokenService();
            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            Mock<ILockoutPolicySettingsRepository> lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            (Mock<IBannedIpMatchService>? bannedIpService, Mock<IThreatIntelBlocklistService>? threatIntelService, Mock<IGeoIpLookupService>? geoIpService) = CreateDefaultGeoAndThreatMocks();

            HttpContext context = CreateHttpContext(NetworkTier.Network);
            LoginPasswordRequest request = CreateLoginRequest(op.Username, "WrongPassword123!");

            // Act
            IResult result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordLoginAttemptAsync(op.Id, "127.0.0.1", false, "Invalid password", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginPasswordAsync_LockedOutAfterThreshold_RecordsLockoutEvent()
        {
            // Arrange - operator at failed attempt threshold; 5th failure triggers lockout
            Operator op = CreateOperator(failedLoginAttemptCount: 4);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            // Simulate IncrementFailedLoginAttemptAsync setting lockout
            _ = operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback((Guid id, int max, int duration, CancellationToken ct) =>
                {
                    op.FailedLoginAttemptCount = max;
                    op.LockedOutUntilUtc = DateTime.UtcNow.AddMinutes(duration);
                })
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            _ = credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            Mock<ISessionTokenService> sessionTokens = MockSessionTokenService();
            Mock<INetworkTierResolver> tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            Mock<ILockoutPolicySettingsRepository> lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            (Mock<IBannedIpMatchService>? bannedIpService, Mock<IThreatIntelBlocklistService>? threatIntelService, Mock<IGeoIpLookupService>? geoIpService) = CreateDefaultGeoAndThreatMocks();

            HttpContext context = CreateHttpContext(NetworkTier.Network);
            LoginPasswordRequest request = CreateLoginRequest(op.Username, "WrongPassword123!");

            // Act
            IResult result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordAccountLockoutAsync(op.Id, "127.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
        }

        // Helper methods
        private static Operator CreateOperator(
            string username = "testuser",
            OperatorRole role = OperatorRole.ReadOnly,
            bool isApproved = true,
            bool active = true,
            int failedLoginAttemptCount = 0,
            DateTime? lockedOutUntilUtc = null)
        {
            var op = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                Username = username,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Role = role,
                IsApproved = isApproved,
                Active = active,
                FailedLoginAttemptCount = failedLoginAttemptCount,
                LockedOutUntilUtc = lockedOutUntilUtc,
                SecurityStamp = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                MustChangePassword = false
            };
            return op;
        }

        private static HttpContext CreateHttpContext(NetworkTier tier = NetworkTier.Network)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = tier == NetworkTier.Local
                ? IPAddress.Loopback
                : IPAddress.Parse("192.168.1.1");
            return context;
        }

        private static Mock<ISessionTokenService> MockSessionTokenService()
        {
            var mock = new Mock<ISessionTokenService>();
            _ = mock.Setup(s => s.Issue(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CredentialKind>(),
                It.IsAny<OperatorRole>(),
                It.IsAny<Guid>()))
                .Returns("valid-session-token");
            return mock;
        }

        private static Mock<INetworkTierResolver> MockNetworkTierResolver(NetworkTier tier)
        {
            var mock = new Mock<INetworkTierResolver>();
            _ = mock.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(tier);
            _ = mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        private static ISessionTierHeaderProtector NoOpHeaderProtector()
        {
            return new Mock<ISessionTierHeaderProtector>().Object;
        }

        private static Mock<ILockoutPolicySettingsRepository> MockLockoutPolicyRepository(
            int maxFailedAttempts = 5,
            int lockoutDurationMinutes = 15)
        {
            var mock = new Mock<ILockoutPolicySettingsRepository>();
            _ = mock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LockoutPolicySettings
                {
                    Id = Guid.NewGuid(),
                    MaxFailedAttempts = maxFailedAttempts,
                    LockoutDurationMinutes = lockoutDurationMinutes,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            return mock;
        }

        private static (Mock<IBannedIpMatchService>, Mock<IThreatIntelBlocklistService>, Mock<IGeoIpLookupService>) CreateDefaultGeoAndThreatMocks()
        {
            var bannedIpService = new Mock<IBannedIpMatchService>();
            _ = bannedIpService.Setup(s => s.IsIpBannedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var threatIntelService = new Mock<IThreatIntelBlocklistService>();
            _ = threatIntelService.Setup(s => s.IsIpBlockedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var geoIpService = new Mock<IGeoIpLookupService>();
            _ = geoIpService.Setup(s => s.LookupCountryCodeAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            return (bannedIpService, threatIntelService, geoIpService);
        }

        private static LoginPasswordRequest CreateLoginRequest(string username, string password)
        {
            return new LoginPasswordRequest(username, password);
        }
    }

    /// <summary>
    /// Helper class to invoke the private LoginPasswordAsync method in OperatorAuthEndpoints for testing.
    /// </summary>
    internal static class OperatorAuthEndpointsInvoker
    {
        public static async Task<IResult> LoginPasswordAsync(
            LoginPasswordRequest request,
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILockoutPolicySettingsRepository lockoutPolicy,
            ITwoFactorRoleRequirementRepository twoFactorRequirements,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            INotificationDispatcher? notificationDispatcher,
            IBannedIpMatchService bannedIpService,
            IThreatIntelBlocklistService threatIntelService,
            IGeoIpLookupService geoIpService,
            ISecurityAuditService auditService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(VideoForensics.WebApp.Api.OperatorAuthEndpoints)
                .GetMethod("LoginPasswordAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(LoginPasswordRequest), typeof(IOperatorRepository), typeof(IOperatorCredentialRepository), typeof(ISessionTokenService),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(ISessionTierHeaderProtector), typeof(ILockoutPolicySettingsRepository),
                     typeof(ITwoFactorRoleRequirementRepository), typeof(ITwoFactorPendingAuthCache),
                     typeof(INotificationDispatcher), typeof(IBannedIpMatchService), typeof(IThreatIntelBlocklistService),
                     typeof(IGeoIpLookupService), typeof(ISecurityAuditService), typeof(IAuthMethodSettingsService), typeof(IFailedLoginIpTracker), typeof(HttpContext), typeof(ILogger<Program>), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find LoginPasswordAsync method");
            }

            var authMethodSettings = new Mock<IAuthMethodSettingsService>();
            _ = authMethodSettings.Setup(s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            IFailedLoginIpTracker failedLoginIpTracker = new Mock<IFailedLoginIpTracker>().Object;
            object? result = method.Invoke(null, [request, operators, credentials, sessionTokens, auditLog, tierResolver, headerProtector, lockoutPolicy, twoFactorRequirements, twoFactorPendingAuthCache, notificationDispatcher, bannedIpService, threatIntelService, geoIpService, auditService, authMethodSettings.Object, failedLoginIpTracker, context, logger, ct]);
            return await (Task<IResult>)result!;
        }
    }
}
