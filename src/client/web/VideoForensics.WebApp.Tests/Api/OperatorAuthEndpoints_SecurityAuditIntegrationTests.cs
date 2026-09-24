using Moq;
using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>Tests for ISecurityAuditService integration in OperatorAuthEndpoints (Phase 0.5).</summary>
    public class OperatorAuthEndpoints_SecurityAuditIntegrationTests
    {
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

        [Fact]
        public async Task UnlockAccount_ReleasesLockout_RecordsReleaseEvent()
        {
            // Arrange - operator is locked out; admin unlocks them
            var operatorId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.UnlockAsync(operatorId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLog = new Mock<ISecurityAuditLogger>();
            var auditService = new Mock<ISecurityAuditService>();
            var tierResolver = new Mock<INetworkTierResolver>();
            tierResolver.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            var context = CreateAuthenticatedHttpContext(adminId);

            // Act
            var result = await DeviceManagementEndpointsInvoker.UnlockAsync(
                operatorId, operators.Object, auditLog.Object, tierResolver.Object, auditService.Object, context, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            auditService.Verify(s => s.RecordAccountLockoutReleasedAsync(operatorId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // Helper methods (stubs - these would use existing test fixtures)
        private static Operator CreateOperator(int failedLoginAttemptCount = 0)
        {
            return new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                Username = "testuser",
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Role = OperatorRole.Admin,
                FailedLoginAttemptCount = failedLoginAttemptCount,
                IsApproved = true,
                Active = true,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid()
            };
        }

        private static HttpContext CreateHttpContext(NetworkTier tier)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            return context;
        }

        private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            context.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("sub", operatorId.ToString()) }));
            return context;
        }

        private static Mock<ISessionTokenService> MockSessionTokenService()
        {
            var mock = new Mock<ISessionTokenService>();
            mock.Setup(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(("test-token", DateTime.UtcNow.AddHours(1)));
            return mock;
        }

        private static Mock<INetworkTierResolver> MockNetworkTierResolver(NetworkTier tier)
        {
            var mock = new Mock<INetworkTierResolver>();
            mock.Setup(r => r.ResolveAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tier);
            mock.Setup(r => r.ResolveClientIp(It.IsAny<HttpContext>()))
                .Returns("127.0.0.1");
            return mock;
        }

        private static Mock<ILockoutPolicyRepository> MockLockoutPolicyRepository()
        {
            var mock = new Mock<ILockoutPolicyRepository>();
            mock.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LockoutPolicySettings { MaxFailedAttempts = 5, LockoutDurationMinutes = 30 });
            return mock;
        }

        private static (Mock<IBannedIpMatchService>, Mock<IThreatIntelBlocklistService>, Mock<IGeoIpLookupService>) CreateDefaultGeoAndThreatMocks()
        {
            var banned = new Mock<IBannedIpMatchService>();
            banned.Setup(s => s.IsIpBannedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var threat = new Mock<IThreatIntelBlocklistService>();
            threat.Setup(s => s.IsThreatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var geo = new Mock<IGeoIpLookupService>();
            geo.Setup(s => s.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeoLocation { CountryCode = "US" });

            return (banned, threat, geo);
        }

        private static LoginRequest CreateLoginRequest(string username, string password)
        {
            return new LoginRequest { Username = username, Password = password };
        }
    }
}
