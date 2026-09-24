using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
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
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            var context = CreateHttpContext(NetworkTier.Network);
            var request = CreateLoginRequest(op.Username, TestPassword);

            // Act
            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordLoginAttemptAsync(op.Id, "127.0.0.1", true, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginPasswordAsync_FailedPassword_RecordsFailureEvent()
        {
            // Arrange - operator exists but password is wrong
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            var context = CreateHttpContext(NetworkTier.Network);
            var request = CreateLoginRequest(op.Username, "WrongPassword123!");

            // Act
            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordLoginAttemptAsync(op.Id, "127.0.0.1", false, "Invalid password", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginPasswordAsync_LockedOutAfterThreshold_RecordsLockoutEvent()
        {
            // Arrange - operator at failed attempt threshold; 5th failure triggers lockout
            var op = CreateOperator(failedLoginAttemptCount: 4);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            // Simulate IncrementFailedLoginAttemptAsync setting lockout
            operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback((Guid id, int max, int duration, CancellationToken ct) =>
                {
                    op.FailedLoginAttemptCount = max;
                    op.LockedOutUntilUtc = DateTime.UtcNow.AddMinutes(duration);
                })
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditService = new Mock<ISecurityAuditService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            var context = CreateHttpContext(NetworkTier.Network);
            var request = CreateLoginRequest(op.Username, "WrongPassword123!");

            // Act
            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, CancellationToken.None);

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
            mock.Setup(s => s.Issue(
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
            mock.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(tier);
            mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            return mock;
        }

        private static Mock<ILockoutPolicySettingsRepository> MockLockoutPolicyRepository(
            int maxFailedAttempts = 5,
            int lockoutDurationMinutes = 15)
        {
            var mock = new Mock<ILockoutPolicySettingsRepository>();
            mock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
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
            bannedIpService.Setup(s => s.IsIpBannedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var threatIntelService = new Mock<IThreatIntelBlocklistService>();
            threatIntelService.Setup(s => s.IsIpBlockedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var geoIpService = new Mock<IGeoIpLookupService>();
            geoIpService.Setup(s => s.LookupCountryCodeAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
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
            ILockoutPolicySettingsRepository lockoutPolicy,
            ITwoFactorRoleRequirementRepository twoFactorRequirements,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            INotificationDispatcher? notificationDispatcher,
            IBannedIpMatchService bannedIpService,
            IThreatIntelBlocklistService threatIntelService,
            IGeoIpLookupService geoIpService,
            ISecurityAuditService auditService,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(VideoForensics.WebApp.Api.OperatorAuthEndpoints)
                .GetMethod("LoginPasswordAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(LoginPasswordRequest), typeof(IOperatorRepository), typeof(IOperatorCredentialRepository), typeof(ISessionTokenService),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(ILockoutPolicySettingsRepository),
                     typeof(ITwoFactorRoleRequirementRepository), typeof(ITwoFactorPendingAuthCache),
                     typeof(INotificationDispatcher), typeof(IBannedIpMatchService), typeof(IThreatIntelBlocklistService),
                     typeof(IGeoIpLookupService), typeof(ISecurityAuditService), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
                throw new InvalidOperationException("Could not find LoginPasswordAsync method");

            var result = method.Invoke(null, [request, operators, credentials, sessionTokens, auditLog, tierResolver, lockoutPolicy, twoFactorRequirements, twoFactorPendingAuthCache, notificationDispatcher, bannedIpService, threatIntelService, geoIpService, auditService, context, ct]);
            return await (Task<IResult>)result!;
        }
    }
}
