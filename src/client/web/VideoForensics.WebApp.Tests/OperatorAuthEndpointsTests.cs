using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class OperatorAuthEndpointsTests
    {
        private static readonly PasswordHasher<Operator> PasswordHasher = new();
        private const string TestPassword = "TestPassword123!";

        private static LoginPasswordRequest CreateLoginRequest(string username, string password)
        {
            return new LoginPasswordRequest(username, password);
        }

        private static HttpContext CreateHttpContext(NetworkTier tier = NetworkTier.Network)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = tier == NetworkTier.Local
                ? System.Net.IPAddress.Loopback
                : System.Net.IPAddress.Parse("192.168.1.1");
            return context;
        }

        private static Operator CreateOperator(
            string username = "testuser",
            OperatorRole role = OperatorRole.ReadOnly,
            bool isPrimarySuperAdmin = false,
            bool isApproved = true,
            bool active = true,
            string? passwordHash = null,
            int failedLoginAttemptCount = 0,
            DateTime? lockedOutUntilUtc = null)
        {
            var op = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test User",
                Username = username,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Role = role,
                IsPrimarySuperAdmin = isPrimarySuperAdmin,
                IsApproved = isApproved,
                Active = active,
                PasswordHash = passwordHash,
                FailedLoginAttemptCount = failedLoginAttemptCount,
                LockedOutUntilUtc = lockedOutUntilUtc,
                SecurityStamp = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                MustChangePassword = false
            };
            return op;
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

        [Fact]
        public async Task LoginPassword_PrimarySuperAdminCorrectPasswordNonLocalTier_ReturnsUnauthorized()
        {
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();

            var context = CreateHttpContext(NetworkTier.Network);
            var request = CreateLoginRequest(op.Username, TestPassword);

            var credentials = new Mock<IOperatorCredentialRepository>();
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_BannedIpAddress_RejectsLoginWithGenericFailure()
        {
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);
            var bannedIp = IPAddress.Parse("203.0.113.50");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns(bannedIp.ToString());

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();

            var bannedIpService = new Mock<IBannedIpMatchService>();
            bannedIpService.Setup(s => s.IsIpBannedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var threatIntelService = new Mock<IThreatIntelBlocklistService>();
            threatIntelService.Setup(s => s.IsIpBlockedAsync(It.IsAny<IPAddress>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var geoIpService = new Mock<IGeoIpLookupService>();
            var auditService = new Mock<ISecurityAuditService>();

            var credentials = new Mock<IOperatorCredentialRepository>();
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var context = CreateHttpContext();
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_WithTwoFactorRequiredAndApprovedCredentials_ReturnsTwoFactorChallenge()
        {
            // Arrange - operator with 2FA required and approved credentials
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] {
                    new OperatorCredential {
                        Id = Guid.NewGuid(),
                        OperatorId = op.Id,
                        Label = "Test Credential",
                        WebAuthnCredentialId = "test-cred-id",
                        WebAuthnPublicKey = System.Text.Encoding.UTF8.GetBytes("test-public-key"),
                        IsApproved = true,
                        RevokedAtUtc = null,
                        CreatedAtUtc = DateTime.UtcNow,
                        WebAuthnSignCount = 0
                    }
                });

            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            twoFactorCache.Setup(c => c.Store(op.Id))
                .Returns("test-correlation-token");

            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();

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
            // Should NOT issue session token - instead return 2FA challenge
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
            twoFactorCache.Verify(c => c.Store(op.Id), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_WithTwoFactorRequiredButNoCredentials_ReturnsSessionTokenWithPasskeyRegistrationFlag()
        {
            // Arrange - operator with 2FA required but no approved credentials (bootstrap path)
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
            credentials.Setup(c => c.ListForOperatorAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());

            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();

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
            // Should issue session token for bootstrap path
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_WithTwoFactorNotRequired_ReturnsSessionTokenNormally()
        {
            // Arrange - operator with 2FA NOT required
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
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();

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
            // Should issue session token immediately
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }
    }

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
            var method = typeof(OperatorAuthEndpoints)
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
            {
                throw new InvalidOperationException("Could not find LoginPasswordAsync method");
            }

            var result = method.Invoke(null, [request, operators, credentials, sessionTokens, auditLog, tierResolver, lockoutPolicy, twoFactorRequirements, twoFactorPendingAuthCache, notificationDispatcher, bannedIpService, threatIntelService, geoIpService, auditService, context, ct]);
            return await (Task<IResult>)result!;
        }
    }
}
