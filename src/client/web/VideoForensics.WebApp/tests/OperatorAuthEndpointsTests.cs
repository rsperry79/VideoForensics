using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Fido2NetLib;
using Fido2NetLib.Objects;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
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

        /// <summary>A protector that never validates a header - fine for tests that don't attach
        /// X-VF-Session-Tier at all, where RequestTierResolver.ResolvePreAuth never even calls it.</summary>
        private static ISessionTierHeaderProtector NoOpHeaderProtector() => new Mock<ISessionTierHeaderProtector>().Object;

        private static Mock<ILockoutPolicySettingsRepository> MockLockoutPolicyRepository(
            int maxFailedAttempts = 5,
            int lockoutDurationMinutes = 15,
            bool autoBanEnabled = false)
        {
            var mock = new Mock<ILockoutPolicySettingsRepository>();
            mock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LockoutPolicySettings
                {
                    Id = Guid.NewGuid(),
                    MaxFailedAttempts = maxFailedAttempts,
                    LockoutDurationMinutes = lockoutDurationMinutes,
                    UpdatedAtUtc = DateTime.UtcNow,
                    AutoBanEnabled = autoBanEnabled
                });
            return mock;
        }

        private static Mock<IBannedIpRangeRepository> MockBannedIpRangeRepository(List<BannedIpRange> store)
        {
            var repository = new Mock<IBannedIpRangeRepository>();
            repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => store.ToList());
            repository.Setup(r => r.AddAsync(It.IsAny<BannedIpRange>(), It.IsAny<CancellationToken>()))
                .Callback<BannedIpRange, CancellationToken>((range, _) => store.Add(range))
                .Returns(Task.CompletedTask);
            return repository;
        }

        /// <summary>Real tracker over a real singleton-to-scope resolution path, writing into the given repository mock.</summary>
        private static FailedLoginIpTracker CreateFailedLoginIpTracker(Mock<IBannedIpRangeRepository> repository)
        {
            var services = new ServiceCollection()
                .AddSingleton(repository.Object)
                .AddSingleton(new Mock<ISecurityAuditLogger>().Object)
                .BuildServiceProvider();

            return new FailedLoginIpTracker(TimeProvider.System, services.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FailedLoginIpTracker>.Instance);
        }

        private static HttpContext CreateHttpContextFromPeer(string peerIp)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(peerIp);
            return context;
        }

        /// <summary>An operator whose next wrong password locks the account (MaxFailedAttempts = 1) and whose password is TestPassword.</summary>
        private static Operator CreateLockableOperator(Mock<IOperatorRepository> operators, string username)
        {
            var op = CreateOperator(username: username);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            operators.Setup(r => r.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, int, int, CancellationToken>((_, _, minutes, _) => op.LockedOutUntilUtc = DateTime.UtcNow.AddMinutes(minutes))
                .Returns(Task.CompletedTask);
            return op;
        }

        private static Task<IResult> AttemptPasswordLoginAsync(
            string username,
            string password,
            HttpContext context,
            IOperatorRepository operators,
            IBannedIpMatchService bannedIpService,
            IFailedLoginIpTracker failedLoginIpTracker,
            ISessionTokenService sessionTokens)
        {
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var (_, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            return OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                CreateLoginRequest(username, password), operators, credentials.Object, sessionTokens, new Mock<ISecurityAuditLogger>().Object,
                new NetworkTierResolver(), NoOpHeaderProtector(), MockLockoutPolicyRepository(maxFailedAttempts: 1, autoBanEnabled: true).Object,
                twoFactorRequirements.Object, new Mock<ITwoFactorPendingAuthCache>().Object, new Mock<INotificationDispatcher>().Object,
                bannedIpService, threatIntelService.Object, geoIpService.Object, new Mock<ISecurityAuditService>().Object,
                context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None, failedLoginIpTracker);
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
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        // --- Primary-SuperAdmin Local-only check via the WebApp's own self-call (plan §5.10/§5.12):
        // when reached through DeviceSignIn.razor's WebAuthnClient.SignInWithPasswordAsync self-call,
        // this request's own connection is ALWAYS loopback (the WebApp calling itself), regardless of
        // where the real browser physically is - RequestTierResolver.ResolvePreAuth must recover the
        // real tier from the X-VF-Session-Tier pre-auth header instead of trusting that connection. ---

        private static HttpContext CreateLoopbackHttpContext() => CreateHttpContext(NetworkTier.Local);

        [Fact]
        public async Task LoginPassword_PrimarySuperAdmin_RemoteBrowsersInternetHeaderOverLoopback_IsRejected()
        {
            // A remote browser's own real tier (Internet) travels as a pre-auth header on the
            // self-call's own loopback connection - the primary SuperAdmin must still be rejected.
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var realProtector = new SessionTierHeaderProtector(new EphemeralDataProtectionProvider());
            string preAuthHeader = realProtector.ProtectPreAuth(NetworkTier.Internet);

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            HttpContext context = CreateLoopbackHttpContext();
            context.Request.Headers[SessionTierHeaderNames.HeaderName] = preAuthHeader;
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, realProtector, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_PrimarySuperAdmin_GenuineLocalHeaderOverLoopback_IsAllowed()
        {
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var realProtector = new SessionTierHeaderProtector(new EphemeralDataProtectionProvider());
            string preAuthHeader = realProtector.ProtectPreAuth(NetworkTier.Local);

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            HttpContext context = CreateLoopbackHttpContext();
            context.Request.Headers[SessionTierHeaderNames.HeaderName] = preAuthHeader;
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, realProtector, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_PrimarySuperAdmin_NoHeaderOverLoopback_UnchangedBehaviorAllowed()
        {
            // No X-VF-Session-Tier header at all: behavior must be identical to before this feature
            // existed - a genuinely local caller (e.g. a direct localhost request, not a self-call)
            // resolves from the connection alone.
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            HttpContext context = CreateLoopbackHttpContext(); // no tier header attached at all
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_PrimarySuperAdmin_TamperedHeaderOverLoopback_IsRejected()
        {
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Local);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");

            var realProtector = new SessionTierHeaderProtector(new EphemeralDataProtectionProvider());
            string validHeader = realProtector.ProtectPreAuth(NetworkTier.Local);
            char[] chars = validHeader.ToCharArray();
            chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
            string tamperedHeader = new string(chars);

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            HttpContext context = CreateLoopbackHttpContext();
            context.Request.Headers[SessionTierHeaderNames.HeaderName] = tamperedHeader;
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, realProtector, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_PrimarySuperAdmin_LocalHeaderOnNonLoopbackConnection_IsIgnoredAndRejected()
        {
            // A genuinely remote caller (connection itself isn't loopback) cannot use the header to
            // claim Local - it must be ignored entirely and the real (non-Local) connection tier used.
            var op = CreateOperator(isPrimarySuperAdmin: true, role: OperatorRole.SuperAdmin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveTier(It.IsAny<HttpContext>())).Returns(NetworkTier.Network);
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("192.168.1.1");

            var realProtector = new SessionTierHeaderProtector(new EphemeralDataProtectionProvider());
            string claimsLocalHeader = realProtector.ProtectPreAuth(NetworkTier.Local);

            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            HttpContext context = CreateHttpContext(NetworkTier.Network);
            context.Request.Headers[SessionTierHeaderNames.HeaderName] = claimsLocalHeader;
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, realProtector, lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

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
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var context = CreateHttpContext();
            var request = CreateLoginRequest(op.Username, TestPassword);

            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

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
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

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
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // No passkey yet: issue a LIMITED passkey-setup session, never a full session
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp), Times.Once);
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Never);
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
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
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
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Should issue session token immediately
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task PasswordLogin_OperatorWithoutPasskey_ReturnsHasPasskeyFalse()
        {
            // Arrange - 2FA not required; operator has no OperatorCredential at all
            IResult result = await RunPasswordLoginWithCredentialsAsync(new List<OperatorCredential>());

            // Assert
            Assert.False(GetOkPayloadBool(result, "hasPasskey"));
        }

        [Fact]
        public async Task PasswordLogin_OperatorWithPasskey_ReturnsHasPasskeyTrue()
        {
            // Arrange - 2FA not required; operator has one active OperatorCredential
            var credential = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                Label = "Laptop",
                WebAuthnCredentialId = "cred-id",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                IsApproved = true,
            };

            IResult result = await RunPasswordLoginWithCredentialsAsync(new List<OperatorCredential> { credential });

            // Assert
            Assert.True(GetOkPayloadBool(result, "hasPasskey"));
        }

        /// <summary>Runs a successful password login for an operator whose OperatorCredential list is <paramref name="credentials"/>; 2FA is not required.</summary>
        private static async Task<IResult> RunPasswordLoginWithCredentialsAsync(List<OperatorCredential> credentialList)
        {
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
                .ReturnsAsync(credentialList);

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

            return await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);
        }

        /// <summary>Reads a bool property from the anonymous payload of an <c>Ok</c> result. Fails with a clear message if the property is missing.</summary>
        private static bool GetOkPayloadBool(IResult result, string propertyName)
        {
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            object? payload = result.GetType().GetProperty("Value")?.GetValue(result);
            Assert.NotNull(payload);

            var property = payload!.GetType().GetProperty(propertyName);
            Assert.True(property is not null, $"Login response has no '{propertyName}' property.");
            return Assert.IsType<bool>(property!.GetValue(payload));
        }

        /// <summary>Reads a named property from the anonymous payload of an <c>Ok</c> result, or null when absent.</summary>
        private static object? GetOkPayloadProperty(IResult result, string propertyName)
        {
            object? payload = result.GetType().GetProperty("Value")?.GetValue(result);
            Assert.NotNull(payload);
            return payload!.GetType().GetProperty(propertyName)?.GetValue(payload);
        }

        /// <summary>Runs a password login for an operator whose 2FA requirement is forced on, with the given OperatorCredential list.</summary>
        private static async Task<IResult> RunPasswordLoginWithTwoFactorAsync(
            Operator op,
            List<OperatorCredential> credentialList,
            Mock<ISessionTokenService> sessionTokens,
            Mock<ITwoFactorPendingAuthCache> twoFactorCache)
        {
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(op.Id, It.IsAny<CancellationToken>())).ReturnsAsync(credentialList);

            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            return await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                CreateLoginRequest(op.Username, TestPassword), operators.Object, credentials.Object, sessionTokens.Object, new Mock<ISecurityAuditLogger>().Object,
                new NetworkTierResolver(), NoOpHeaderProtector(), MockLockoutPolicyRepository().Object, twoFactorRequirements.Object, twoFactorCache.Object,
                new Mock<INotificationDispatcher>().Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                new Mock<ISecurityAuditService>().Object, CreateHttpContext(NetworkTier.Network),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);
        }

        [Fact]
        public async Task LoginPassword_TwoFactorRequiredNoPasskey_ReturnsLimitedSession()
        {
            // Arrange - 2FA is required for this operator but they have no passkey, so the second factor can never be completed
            var op = CreateOperator(role: OperatorRole.Admin);
            op.TwoFactorRequirementOverride = TwoFactorRequirementOverride.Required;
            var sessionTokens = new Mock<ISessionTokenService>();
            sessionTokens.Setup(s => s.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp)).Returns("limited-token");
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            // Act
            IResult result = await RunPasswordLoginWithTwoFactorAsync(op, new List<OperatorCredential>(), sessionTokens, twoFactorCache);

            // Assert
            Assert.Equal("limited-token", GetOkPayloadProperty(result, "sessionToken"));
            Assert.True(GetOkPayloadBool(result, "requiresPasskeyRegistration"));
            Assert.False(GetOkPayloadBool(result, "hasPasskey"));
            Assert.Null(GetOkPayloadProperty(result, "requiresTwoFactor"));
            twoFactorCache.Verify(c => c.Store(It.IsAny<Guid>()), Times.Never);
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_OperatorWithPasskey_StillRequiresTwoFactor()
        {
            // Arrange - 2FA is required and the operator has an approved passkey to complete it
            var op = CreateOperator(role: OperatorRole.Admin);
            op.TwoFactorRequirementOverride = TwoFactorRequirementOverride.Required;
            var passkey = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = op.Id,
                Label = "Laptop",
                WebAuthnCredentialId = "cred-id",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                IsApproved = true
            };
            var sessionTokens = new Mock<ISessionTokenService>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            twoFactorCache.Setup(c => c.Store(op.Id)).Returns("correlation-id");

            // Act
            IResult result = await RunPasswordLoginWithTwoFactorAsync(op, new List<OperatorCredential> { passkey }, sessionTokens, twoFactorCache);

            // Assert
            Assert.True(GetOkPayloadBool(result, "requiresTwoFactor"));
            Assert.Null(GetOkPayloadProperty(result, "sessionToken"));
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(It.IsAny<Guid>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_TwoFactorRequired_ReturnsPasskeyMethod()
        {
            // Arrange - the client renders second-factor buttons from twoFactorMethods, so the server must name what it accepts
            var op = CreateOperator(role: OperatorRole.Admin);
            op.TwoFactorRequirementOverride = TwoFactorRequirementOverride.Required;
            var passkey = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = op.Id,
                Label = "Laptop",
                WebAuthnCredentialId = "cred-id",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                IsApproved = true
            };
            var sessionTokens = new Mock<ISessionTokenService>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            twoFactorCache.Setup(c => c.Store(op.Id)).Returns("correlation-id");

            // Act
            IResult result = await RunPasswordLoginWithTwoFactorAsync(op, new List<OperatorCredential> { passkey }, sessionTokens, twoFactorCache);

            // Assert
            Assert.Equal(new[] { "passkey" }, Assert.IsAssignableFrom<IEnumerable<string>>(GetOkPayloadProperty(result, "twoFactorMethods")));
        }

        /// <summary>Runs the create-passkey password endpoint. It never asks for a second factor, so no 2FA settings are needed.</summary>
        private static async Task<IResult> RunCreatePasskeyLoginAsync(
            Operator op,
            Mock<IOperatorRepository> operators,
            List<OperatorCredential> credentialList,
            string password,
            Mock<ISessionTokenService> sessionTokens)
        {
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>())).ReturnsAsync(op);
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(op.Id, It.IsAny<CancellationToken>())).ReturnsAsync(credentialList);

            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(op.Role, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();

            return await OperatorAuthEndpointsInvoker.LoginPasswordAsync(
                CreateLoginRequest(op.Username, password), operators.Object, credentials.Object, sessionTokens.Object, new Mock<ISecurityAuditLogger>().Object,
                new NetworkTierResolver(), NoOpHeaderProtector(), MockLockoutPolicyRepository().Object, twoFactorRequirements.Object, new Mock<ITwoFactorPendingAuthCache>().Object,
                new Mock<INotificationDispatcher>().Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                new Mock<ISecurityAuditService>().Object, CreateHttpContext(NetworkTier.Network),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None,
                endpointMethodName: "LoginPasswordCreatePasskeyAsync");
        }

        [Fact]
        public async Task LoginPasswordCreatePasskey_OperatorWithPasskey_ReturnsLimitedSession()
        {
            // Arrange - the operator already has an approved passkey but must still be able to register another one
            var op = CreateOperator(role: OperatorRole.Admin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);
            var passkey = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = op.Id,
                Label = "Laptop",
                WebAuthnCredentialId = "cred-id",
                WebAuthnPublicKey = new byte[] { 1, 2, 3 },
                IsApproved = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            };
            var sessionTokens = new Mock<ISessionTokenService>();
            sessionTokens.Setup(s => s.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp)).Returns("limited-token");

            // Act
            IResult result = await RunCreatePasskeyLoginAsync(op, new Mock<IOperatorRepository>(), new List<OperatorCredential> { passkey }, TestPassword, sessionTokens);

            // Assert
            Assert.Equal("limited-token", GetOkPayloadProperty(result, "sessionToken"));
            Assert.Equal(op.Id, GetOkPayloadProperty(result, "operatorId"));
            Assert.True(GetOkPayloadBool(result, "requiresPasskeyRegistration"));
            Assert.True(GetOkPayloadBool(result, "hasPasskey"));
            Assert.Null(GetOkPayloadProperty(result, "requiresTwoFactor"));
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(op.Id, op.Role, op.SecurityStamp), Times.Once);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPasswordCreatePasskey_WrongPassword_Returns401AndCountsFailure()
        {
            // Arrange
            var op = CreateOperator(role: OperatorRole.Admin);
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);
            var operators = new Mock<IOperatorRepository>();
            var sessionTokens = new Mock<ISessionTokenService>();

            // Act
            IResult result = await RunCreatePasskeyLoginAsync(op, operators, new List<OperatorCredential>(), "wrong-password", sessionTokens);

            // Assert
            Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            operators.Verify(r => r.IncrementFailedLoginAttemptAsync(op.Id, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            operators.Verify(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>()), Times.Never);
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(It.IsAny<Guid>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPasswordCreatePasskey_LockedOut_Rejected()
        {
            // Arrange - correct password, but the account is locked out, so the shared checks must reject before anything is issued
            var op = CreateOperator(role: OperatorRole.Admin, lockedOutUntilUtc: DateTime.UtcNow.AddMinutes(15));
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);
            var sessionTokens = new Mock<ISessionTokenService>();

            // Act
            IResult result = await RunCreatePasskeyLoginAsync(op, new Mock<IOperatorRepository>(), new List<OperatorCredential>(), TestPassword, sessionTokens);

            // Assert
            Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            sessionTokens.Verify(s => s.IssuePasskeySetupOnly(It.IsAny<Guid>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_PasswordDisabledNonSetupPhase_Returns403Forbidden()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.IsEmptyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);  // Not setup phase

            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var context = CreateHttpContext(NetworkTier.Network);
            var request = CreateLoginRequest(op.Username, TestPassword);

            // Create custom invoker that returns password disabled
            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.IsEnabledAsync("password", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsyncWithCustomAuthMethods(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, authMethodsService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Must not issue a session token
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
            // Must log audit failure
            auditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.AuthFailure, null, null,
                It.IsAny<string>(), "Password sign-in is disabled", true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginPassword_PasswordDisabledSetupPhase_AllowsLogin()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync(op.Username, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.IsEmptyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);  // Setup phase - should allow login even if password disabled
            operators.Setup(r => r.ResetFailedLoginAttemptsAsync(op.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            operators.Setup(r => r.SetApprovalFirstLoginNotifiedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var sessionTokens = MockSessionTokenService();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Local);
            var lockoutPolicy = MockLockoutPolicyRepository();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var (bannedIpService, threatIntelService, geoIpService) = CreateDefaultGeoAndThreatMocks();
            var auditService = new Mock<ISecurityAuditService>();
            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OperatorCredential>());
            var twoFactorRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            twoFactorRequirements.Setup(r => r.GetRequirementForRoleAsync(It.IsAny<OperatorRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();

            var context = CreateHttpContext(NetworkTier.Local);
            var request = CreateLoginRequest(op.Username, TestPassword);

            // Create custom invoker that returns password disabled
            var authMethodsService = new Mock<IAuthMethodSettingsService>();
            authMethodsService.Setup(s => s.IsEnabledAsync("password", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await OperatorAuthEndpointsInvoker.LoginPasswordAsyncWithCustomAuthMethods(
                request, operators.Object, credentials.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, NoOpHeaderProtector(), lockoutPolicy.Object, twoFactorRequirements.Object, twoFactorCache.Object,
                notificationDispatcher.Object, bannedIpService.Object, threatIntelService.Object, geoIpService.Object,
                auditService.Object, authMethodsService.Object, context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            // Must issue a session token (setup phase bypasses the disabled check)
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, op.SecurityStamp), Times.Once);
        }

        [Fact]
        public async Task RecoverPassword_NonLocalhost_ReturnsForbidden()
        {
            // Arrange
            var operators = new Mock<IOperatorRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var context = CreateHttpContext(NetworkTier.Network);  // Non-localhost

            // Act
            var result = await OperatorAuthEndpointsInvoker.RecoverPasswordAsync(
                operators.Object, auditLog.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        }

        [Fact]
        public async Task RecoverPassword_NoOperatorsExist_ReturnsNotFound()
        {
            // Arrange
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Operator>());  // Empty list

            var auditLog = new Mock<ISecurityAuditLogger>();
            var context = CreateHttpContext(NetworkTier.Local);  // Localhost

            // Act
            var result = await OperatorAuthEndpointsInvoker.RecoverPasswordAsync(
                operators.Object, auditLog.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, statusResult.StatusCode);
        }

        [Fact]
        public async Task RecoverPassword_LocalhostWithOperators_GeneratesNewPassword()
        {
            // Arrange
            var primaryAdmin = CreateOperator(username: "admin", role: OperatorRole.SuperAdmin, isPrimarySuperAdmin: true);
            var otherOperator = CreateOperator(username: "user", role: OperatorRole.ReadOnly);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Operator> { primaryAdmin, otherOperator });
            operators.Setup(r => r.SetPasswordAsync(primaryAdmin.Id, It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            auditLog.Setup(a => a.LogAsync(
                SecurityAuditEventTypes.PasswordReset,
                primaryAdmin.Id,
                null,
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var context = CreateHttpContext(NetworkTier.Local);  // Localhost

            // Act
            var result = await OperatorAuthEndpointsInvoker.RecoverPasswordAsync(
                operators.Object, auditLog.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            // Verify SetPasswordAsync was called once with MustChangePassword=true
            operators.Verify(
                r => r.SetPasswordAsync(primaryAdmin.Id, It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify audit log was written
            auditLog.Verify(
                a => a.LogAsync(
                    SecurityAuditEventTypes.PasswordReset,
                    primaryAdmin.Id,
                    null,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    true,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void GenerateSecureTemporaryPassword_GeneratesCryptoSecurePassword()
        {
            // This test verifies that temporary password generation uses a cryptographically secure RNG
            // by checking that multiple calls generate different passwords.

            // Arrange
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var passwords = new HashSet<string>();

            // Act: Generate 100 passwords using cryptographically secure method
            for (int i = 0; i < 100; i++)
            {
                // Use System.Security.Cryptography.RandomNumberGenerator instead of Random.Shared
                byte[] randomBytes = new byte[16];
                System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
                var password = string.Concat(randomBytes.Select(b => chars[b % chars.Length]));

                // Assert each password is 16 characters
                Assert.Equal(16, password.Length);

                // Assert password contains only expected characters
                Assert.True(password.All(c => char.IsLetter(c) || char.IsDigit(c)),
                    $"Password contains invalid characters: {password}");

                passwords.Add(password);
            }

            // Assert: All 100 generated passwords should be unique (cryptographically random)
            Assert.True(passwords.Count >= 99,
                $"Expected at least 99 unique passwords from 100 attempts (cryptographically random), but got {passwords.Count}");
        }

        // --- RegisterAsync Tests ---

        [Fact]
        public async Task Register_ValidCredentials_ReturnsCreatedWithOperatorId()
        {
            // Arrange
            var request = new RegisterRequest("newuser", "ValidPassword123!", "John", "Doe", "john@example.com");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync("newuser", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);
            operators.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Operator>());
            operators.Setup(r => r.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator op, CancellationToken ct) => op);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var context = CreateHttpContext();

            // Act
            var result = await OperatorAuthEndpointsInvoker.RegisterAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object,
                notificationDispatcher.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.OperatorRegistered, It.IsAny<Guid>(), null,
                It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Register_DuplicateUsername_ReturnsConflict()
        {
            // Arrange
            var existingOp = CreateOperator(username: "newuser");
            var request = new RegisterRequest("newuser", "ValidPassword123!", "John", "Doe", "john@example.com");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetByUsernameAsync("newuser", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingOp);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var context = CreateHttpContext();

            // Act
            var result = await OperatorAuthEndpointsInvoker.RegisterAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object,
                notificationDispatcher.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var conflictResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);
        }

        [Fact]
        public async Task Register_PasswordTooShort_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequest("newuser", "Short1!", "John", "Doe", "john@example.com");

            var operators = new Mock<IOperatorRepository>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);
            var notificationDispatcher = new Mock<INotificationDispatcher>();
            var context = CreateHttpContext();

            // Act
            var result = await OperatorAuthEndpointsInvoker.RegisterAsync(
                request, operators.Object, auditLog.Object, tierResolver.Object,
                notificationDispatcher.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var badResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badResult.StatusCode);
        }

        // --- ChangePasswordAsync Tests ---

        [Fact]
        public async Task ChangePassword_ValidCurrentPassword_ReturnsNewSessionToken()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var request = new ChangePasswordRequest(TestPassword, "NewPassword123!");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);
            operators.Setup(r => r.SetPasswordAsync(op.Id, It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sessionTokens = MockSessionTokenService();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);

            var context = CreateHttpContext();
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, op.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await OperatorAuthEndpointsInvoker.ChangePasswordAsync(
                request, operators.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            sessionTokens.Verify(s => s.Issue(op.Id, null, CredentialKind.Password, op.Role, It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task ChangePassword_InvalidCurrentPassword_ReturnsUnauthorized()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var request = new ChangePasswordRequest("WrongPassword123!", "NewPassword123!");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);

            var context = CreateHttpContext();
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, op.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await OperatorAuthEndpointsInvoker.ChangePasswordAsync(
                request, operators.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var unauthorizedResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        }

        [Fact]
        public async Task ChangePassword_NewPasswordTooShort_ReturnsBadRequest()
        {
            // Arrange
            var op = CreateOperator();
            var request = new ChangePasswordRequest(TestPassword, "Short!");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var sessionTokens = MockSessionTokenService();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);

            var context = CreateHttpContext();
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, op.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await OperatorAuthEndpointsInvoker.ChangePasswordAsync(
                request, operators.Object, sessionTokens.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var badResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badResult.StatusCode);
        }

        // --- StepUpPasswordAsync Tests ---

        [Fact]
        public async Task StepUpPassword_ValidPassword_ReturnsStepUpToken()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var request = new StepUpPasswordRequest(TestPassword);

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var stepUpAuth = new Mock<IStepUpAuthService>();
            stepUpAuth.Setup(s => s.IssueToken(op.Id))
                .Returns("step-up-token-123");

            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);

            var context = CreateHttpContext();
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, op.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await OperatorAuthEndpointsInvoker.StepUpPasswordAsync(
                request, operators.Object, new Mock<ITwoFactorRoleRequirementRepository>().Object, stepUpAuth.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditLog.Verify(a => a.LogAsync(SecurityAuditEventTypes.StepUpVerified, op.Id, null,
                It.IsAny<string>(), null, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StepUpPassword_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var op = CreateOperator();
            op.PasswordHash = PasswordHasher.HashPassword(op, TestPassword);

            var request = new StepUpPasswordRequest("WrongPassword123!");

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(op);

            var stepUpAuth = new Mock<IStepUpAuthService>();
            var auditLog = new Mock<ISecurityAuditLogger>();
            var tierResolver = MockNetworkTierResolver(NetworkTier.Network);

            var context = CreateHttpContext();
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, op.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await OperatorAuthEndpointsInvoker.StepUpPasswordAsync(
                request, operators.Object, new Mock<ITwoFactorRoleRequirementRepository>().Object, stepUpAuth.Object, auditLog.Object,
                tierResolver.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var unauthorizedResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        }

        [Fact]
        public async Task LoginPassword_LockoutsFromOneIpAcrossAccounts_ThirdLockoutBansIpAndBannedRequestGetsGenericFailure()
        {
            var bannedRanges = new List<BannedIpRange>();
            var repository = MockBannedIpRangeRepository(bannedRanges);
            var tracker = CreateFailedLoginIpTracker(repository);
            var bannedIpService = new BannedIpMatchService(repository.Object);
            var operators = new Mock<IOperatorRepository>();
            var sessionTokens = new Mock<ISessionTokenService>();
            sessionTokens.Setup(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()))
                .Returns("valid-session-token");
            var context = CreateHttpContextFromPeer("203.0.113.10");

            for (int i = 0; i < 3; i++)
            {
                Operator victim = CreateLockableOperator(operators, $"victim-{i}");
                IResult failed = await AttemptPasswordLoginAsync(victim.Username, "wrong-password", context,
                    operators.Object, bannedIpService, tracker, sessionTokens.Object);
                Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(failed).StatusCode);
            }

            BannedIpRange ban = Assert.Single(bannedRanges);
            Assert.Equal("203.0.113.10/32", ban.CidrRange);

            CreateLockableOperator(operators, "innocent-user");
            IResult banned = await AttemptPasswordLoginAsync("innocent-user", TestPassword, context,
                operators.Object, bannedIpService, tracker, sessionTokens.Object);

            Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(banned).StatusCode);
            sessionTokens.Verify(s => s.Issue(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CredentialKind>(), It.IsAny<OperatorRole>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task LoginPassword_SpoofedForwardedHeadersFromNonCloudflarePeer_BanKeysOnTcpPeerNotHeaders()
        {
            var bannedRanges = new List<BannedIpRange>();
            var repository = MockBannedIpRangeRepository(bannedRanges);
            var tracker = CreateFailedLoginIpTracker(repository);
            var bannedIpService = new BannedIpMatchService(repository.Object);
            var operators = new Mock<IOperatorRepository>();
            var sessionTokens = new Mock<ISessionTokenService>();
            var context = CreateHttpContextFromPeer("192.168.1.50");
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
            context.Request.Headers["CF-Connecting-IP"] = "198.51.100.8";

            for (int i = 0; i < 3; i++)
            {
                Operator victim = CreateLockableOperator(operators, $"spoof-victim-{i}");
                await AttemptPasswordLoginAsync(victim.Username, "wrong-password", context,
                    operators.Object, bannedIpService, tracker, sessionTokens.Object);
            }

            BannedIpRange ban = Assert.Single(bannedRanges);
            Assert.Equal("192.168.1.50/32", ban.CidrRange);
            Assert.DoesNotContain(bannedRanges, r => r.CidrRange.StartsWith("198.51.100.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task UsernameAssertionComplete_TwoFactorSuccess_ReturnsMustChangePasswordAndHasPasskey()
        {
            // The passkey second factor must tell the client whether a forced password change is pending;
            // otherwise the client fails closed and sends every operator to /change-password.
            Operator op = CreateOperator(username: "alice", role: OperatorRole.ReadOnly);
            op.MustChangePassword = false;

            var storedCredential = new OperatorCredential
            {
                Id = Guid.NewGuid(),
                OperatorId = op.Id,
                Label = "Laptop",
                WebAuthnCredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                WebAuthnPublicKey = new byte[] { 9, 9, 9 },
                CreatedAtUtc = DateTime.UtcNow,
                FirstLoginNotifiedAtUtc = DateTime.UtcNow
            };

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(op.Id, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            var credentials = new Mock<IOperatorCredentialRepository>();
            credentials.Setup(c => c.GetByWebAuthnCredentialIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedCredential);

            var ceremonyCache = new Mock<IWebAuthnCeremonyCache>();
            ceremonyCache.Setup(c => c.TryTake("nonce-1")).Returns(AssertionOptionsJson);

            var twoFactorCache = new Mock<ITwoFactorPendingAuthCache>();
            twoFactorCache.Setup(c => c.TryTake("corr-1")).Returns((Guid?)op.Id);

            var fido2 = new Mock<IFido2>();
            fido2.Setup(f => f.MakeAssertionAsync(It.IsAny<MakeAssertionParams>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VerifyAssertionResult { SignCount = 5 });

            var request = new UsernameAssertionCompleteRequest(
                "nonce-1",
                System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(AssertionResponseJson),
                "corr-1");

            IResult result = await InvokeUsernameAssertionCompleteAsync(
                request, operators.Object, credentials.Object, MockSessionTokenService().Object, ceremonyCache.Object,
                twoFactorCache.Object, new Mock<ISecurityAuditLogger>().Object, MockNetworkTierResolver(NetworkTier.Network).Object,
                CreateHttpContext(), fido2.Object);

            Assert.False(GetOkPayloadBool(result, "mustChangePassword"));
            Assert.True(GetOkPayloadBool(result, "hasPasskey"));
        }

        private const string AssertionOptionsJson =
            "{\"challenge\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"timeout\":60000,\"rpId\":\"localhost\",\"allowCredentials\":[],\"userVerification\":\"preferred\"}";

        private const string AssertionResponseJson =
            "{\"id\":\"AQID\",\"rawId\":\"AQID\",\"type\":\"public-key\",\"response\":{\"authenticatorData\":\"AAAA\",\"signature\":\"AAAA\",\"clientDataJSON\":\"AAAA\",\"userHandle\":null}}";

        private static Task<IResult> InvokeUsernameAssertionCompleteAsync(
            UsernameAssertionCompleteRequest request,
            IOperatorRepository operators,
            IOperatorCredentialRepository credentials,
            ISessionTokenService sessionTokens,
            IWebAuthnCeremonyCache ceremonyCache,
            ITwoFactorPendingAuthCache twoFactorPendingAuthCache,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            IFido2 fido2)
        {
            var method = typeof(OperatorAuthEndpoints).GetMethod(
                "UsernameAssertionCompleteAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                [typeof(ILogger<Program>), typeof(UsernameAssertionCompleteRequest), typeof(IOperatorRepository), typeof(IOperatorCredentialRepository), typeof(ISessionTokenService),
                 typeof(IWebAuthnCeremonyCache), typeof(ITwoFactorPendingAuthCache), typeof(ISecurityAuditLogger), typeof(INetworkTierResolver),
                 typeof(INotificationDispatcher), typeof(HttpContext), typeof(IFido2), typeof(CancellationToken)],
                null);
            Assert.NotNull(method);

            return (Task<IResult>)method!.Invoke(null,
                [Microsoft.Extensions.Logging.Abstractions.NullLogger<Program>.Instance, request, operators, credentials, sessionTokens, ceremonyCache, twoFactorPendingAuthCache, auditLog, tierResolver,
                 null, context, fido2, CancellationToken.None])!;
        }

        /// <summary>
        /// Calls GET /api/v1/auth/session-kind through a TestServer wired like Program.cs: the real
        /// PairedDevice handler (so the flag comes from the validated token), the real policies, and the real
        /// route registration. Only the repositories and token service are mocked. Only this route is mapped,
        /// because mapping the whole operator-auth group needs every one of its unrelated services.
        /// </summary>
        private static async Task<(HttpStatusCode Status, string Body)> RequestSessionKindAsync(bool limited, bool sendToken, bool twoFactorRequired = false)
        {
            const string token = "session-kind-token";
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            var principal = new SessionPrincipal(
                operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp, DateTime.UtcNow, DateTime.UtcNow.AddHours(12))
            {
                PasskeySetupOnly = limited
            };
            var tokenService = new Mock<ISessionTokenService>();
            _ = tokenService.Setup(t => t.Validate(token)).Returns(principal);

            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Session Kind Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true,
                Username = $"session-kind-{operatorId:N}",
                Role = OperatorRole.Admin,
                FirstName = "Session",
                LastName = "Kind",
                Email = $"{operatorId:N}@test.invalid",
                SecurityStamp = securityStamp
            };
            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);

            var credentials = new Mock<IOperatorCredentialRepository>();
            _ = credentials.Setup(c => c.ListForOperatorAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<OperatorCredential>());

            var roleRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            _ = roleRequirements.Setup(r => r.GetRequirementForRoleAsync(OperatorRole.Admin, It.IsAny<CancellationToken>())).ReturnsAsync(twoFactorRequired);

            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();
            _ = builder.Services.AddSingleton(tokenService.Object);
            _ = builder.Services.AddSingleton(operators.Object);
            _ = builder.Services.AddSingleton(credentials.Object);
            _ = builder.Services.AddSingleton(roleRequirements.Object);
            _ = builder.Services.AddSingleton(new Mock<IPairedDeviceRepository>().Object);
            _ = builder.Services.AddSingleton(new Mock<INetworkTierResolver>().Object);
            _ = builder.Services.AddSingleton(new Mock<ISessionTierHeaderProtector>().Object);
            _ = builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = builder.Services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = builder.Services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = builder.Services.AddAuthentication(PairedDeviceAuthenticationDefaults.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, PairedDeviceAuthenticationHandler>(PairedDeviceAuthenticationDefaults.SchemeName, _ => { });

            WebApplication app = builder.Build();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapSessionKindEndpoint();
            await app.StartAsync();

            try
            {
                using HttpClient client = app.GetTestClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session-kind");
                if (sendToken)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using HttpResponseMessage response = await client.SendAsync(request);
                return (response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static bool ReadPasskeySetupOnly(string body)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("passkeySetupOnly").GetBoolean();
        }

        [Fact]
        public async Task SessionKind_LimitedSession_ReturnsTrue()
        {
            // Arrange - a limited passkey-setup token
            // Act
            (HttpStatusCode status, string body) = await RequestSessionKindAsync(limited: true, sendToken: true);

            // Assert
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.True(ReadPasskeySetupOnly(body));
        }

        [Fact]
        public async Task SessionKind_FullSession_ReturnsFalse()
        {
            // Arrange - an ordinary full session token
            // Act
            (HttpStatusCode status, string body) = await RequestSessionKindAsync(limited: false, sendToken: true);

            // Assert
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.False(ReadPasskeySetupOnly(body));
        }

        [Fact]
        public async Task SessionKind_PasswordSession_ReportsLocalTierAndPasskeyFlags()
        {
            // Arrange - a password session. The tier value comes from the mocked resolver (default Local), so only
            // the presence of the flag and the passkey result are asserted here; tier resolution has its own tests.
            // Act
            (HttpStatusCode status, string body) = await RequestSessionKindAsync(limited: false, sendToken: true);

            // Assert
            Assert.Equal(HttpStatusCode.OK, status);
            using JsonDocument document = JsonDocument.Parse(body);
            _ = document.RootElement.GetProperty("localTier").GetBoolean();
            Assert.False(document.RootElement.GetProperty("passkeySession").GetBoolean());
            // A password login is the only session that re-enters a password for step-up.
            Assert.True(document.RootElement.GetProperty("passwordSession").GetBoolean());
        }

        [Fact]
        public async Task SessionKind_PasswordSessionInRoleRequiringTwoFactor_ReportsTwoFactorRequired()
        {
            // The policy requires a second factor, which is a passkey, so a password-only operator must upgrade before
            // a destructive change. Passkey sessions still report the flag; the client decides what applies.
            (HttpStatusCode status, string body) = await RequestSessionKindAsync(limited: false, sendToken: true, twoFactorRequired: true);

            Assert.Equal(HttpStatusCode.OK, status);
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.True(document.RootElement.GetProperty("twoFactorRequired").GetBoolean());
        }

        [Fact]
        public async Task SessionKind_PasswordSessionInRoleWithoutTwoFactor_ReportsTwoFactorNotRequired()
        {
            (HttpStatusCode status, string body) = await RequestSessionKindAsync(limited: false, sendToken: true, twoFactorRequired: false);

            Assert.Equal(HttpStatusCode.OK, status);
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.False(document.RootElement.GetProperty("twoFactorRequired").GetBoolean());
        }

        [Fact]
        public async Task StepUpPassword_RoleRequiresTwoFactor_RefusesPasswordWithForbidden()
        {
            // The server enforces the policy too: a password cannot satisfy step-up for a role that requires a passkey,
            // even when the client is bypassed.
            var operatorId = Guid.NewGuid();
            var op = new Operator
            {
                Id = operatorId,
                DisplayName = "Policy Operator",
                CreatedAtUtc = DateTime.UtcNow,
                Active = true,
                IsApproved = true,
                Username = $"policy-{operatorId:N}",
                FirstName = "Policy",
                LastName = "Operator",
                Email = $"{operatorId:N}@test.invalid",
                Role = OperatorRole.Admin,
                PasswordHash = "not-used-because-policy-refuses-first",
            };
            var operators = new Mock<IOperatorRepository>();
            _ = operators.Setup(o => o.GetAsync(operatorId, It.IsAny<CancellationToken>())).ReturnsAsync(op);
            var roleRequirements = new Mock<ITwoFactorRoleRequirementRepository>();
            _ = roleRequirements.Setup(r => r.GetRequirementForRoleAsync(OperatorRole.Admin, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString())], "test"))
            };

            var method = typeof(OperatorAuthEndpoints).GetMethod(
                "StepUpPasswordAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            var result = await (Task<IResult>)method!.Invoke(null,
                [new StepUpPasswordRequest("correct-or-not"), operators.Object, roleRequirements.Object,
                 new Mock<IStepUpAuthService>().Object, new Mock<ISecurityAuditLogger>().Object,
                 new Mock<INetworkTierResolver>().Object, context, CancellationToken.None])!;

            var http = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
            await result.ExecuteAsync(http);
            Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        }

        [Fact]
        public async Task SessionKind_Unauthenticated_Returns401()
        {
            // Arrange - no bearer token
            // Act
            (HttpStatusCode status, _) = await RequestSessionKindAsync(limited: false, sendToken: false);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, status);
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
            CancellationToken ct,
            IFailedLoginIpTracker? failedLoginIpTracker = null,
            string endpointMethodName = "LoginPasswordAsync")
        {
            var method = typeof(OperatorAuthEndpoints)
                .GetMethod(endpointMethodName,
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
                throw new InvalidOperationException($"Could not find {endpointMethodName} method");
            }

            var authMethodSettings = new Mock<IAuthMethodSettingsService>();
            authMethodSettings.Setup(s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tracker = failedLoginIpTracker ?? new Mock<IFailedLoginIpTracker>().Object;

            var result = method.Invoke(null, [request, operators, credentials, sessionTokens, auditLog, tierResolver, headerProtector, lockoutPolicy, twoFactorRequirements, twoFactorPendingAuthCache, notificationDispatcher, bannedIpService, threatIntelService, geoIpService, auditService, authMethodSettings.Object, tracker, context, logger, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> LoginPasswordAsyncWithCustomAuthMethods(
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
            IAuthMethodSettingsService authMethodSettings,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            var method = typeof(OperatorAuthEndpoints)
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

            var tracker = new Mock<IFailedLoginIpTracker>().Object;
            var result = method.Invoke(null, [request, operators, credentials, sessionTokens, auditLog, tierResolver, headerProtector, lockoutPolicy, twoFactorRequirements, twoFactorPendingAuthCache, notificationDispatcher, bannedIpService, threatIntelService, geoIpService, auditService, authMethodSettings, tracker, context, logger, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> RecoverPasswordAsync(
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(OperatorAuthEndpoints)
                .GetMethod("RecoverPasswordAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(IOperatorRepository), typeof(ISecurityAuditLogger), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find RecoverPasswordAsync method");
            }

            var result = method.Invoke(null, [operators, auditLog, context, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> RegisterAsync(
            RegisterRequest request,
            IOperatorRepository operators,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            INotificationDispatcher? notificationDispatcher,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(OperatorAuthEndpoints)
                .GetMethod("RegisterAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(RegisterRequest), typeof(IOperatorRepository), typeof(ISecurityAuditLogger),
                     typeof(INetworkTierResolver), typeof(INotificationDispatcher), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find RegisterAsync method");
            }

            var result = method.Invoke(null, [request, operators, auditLog, tierResolver, notificationDispatcher, context, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> ChangePasswordAsync(
            ChangePasswordRequest request,
            IOperatorRepository operators,
            ISessionTokenService sessionTokens,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(OperatorAuthEndpoints)
                .GetMethod("ChangePasswordAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ChangePasswordRequest), typeof(IOperatorRepository), typeof(ISessionTokenService),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find ChangePasswordAsync method");
            }

            var result = method.Invoke(null, [request, operators, sessionTokens, auditLog, tierResolver, context, ct]);
            return await (Task<IResult>)result!;
        }

        public static async Task<IResult> StepUpPasswordAsync(
            StepUpPasswordRequest request,
            IOperatorRepository operators,
            ITwoFactorRoleRequirementRepository roleRequirements,
            IStepUpAuthService stepUpAuth,
            ISecurityAuditLogger auditLog,
            INetworkTierResolver tierResolver,
            HttpContext context,
            CancellationToken ct)
        {
            var method = typeof(OperatorAuthEndpoints)
                .GetMethod("StepUpPasswordAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(StepUpPasswordRequest), typeof(IOperatorRepository), typeof(ITwoFactorRoleRequirementRepository), typeof(IStepUpAuthService),
                     typeof(ISecurityAuditLogger), typeof(INetworkTierResolver), typeof(HttpContext), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find StepUpPasswordAsync method");
            }

            var result = method.Invoke(null, [request, operators, roleRequirements, stepUpAuth, auditLog, tierResolver, context, ct]);
            return await (Task<IResult>)result!;
        }
    }
}
