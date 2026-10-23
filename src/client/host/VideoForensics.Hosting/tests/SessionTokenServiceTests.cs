using Microsoft.AspNetCore.DataProtection;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests
{
    public class SessionTokenServiceTests
    {
        // ============ Issue() Tests ============

        [Fact]
        public void Issue_WithServiceDeviceKind_CreatesEncryptedToken()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            CredentialKind kind = CredentialKind.ServiceDevice;
            OperatorRole role = OperatorRole.Admin;

            // Act
            string token = service.Issue(operatorId, credentialId: null, kind, role, securityStamp);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
            // Token should be opaque (protected) string, not JSON
            Assert.False(token.Contains("{"));
        }

        [Fact]
        public void Issue_WithServiceDeviceKind_TokenContainsAllFields()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            CredentialKind kind = CredentialKind.ServiceDevice;
            OperatorRole role = OperatorRole.Admin;

            // Act
            string token = service.Issue(operatorId, credentialId: null, kind, role, securityStamp);
            SessionPrincipal? validated = service.Validate(token);

            // Assert
            Assert.NotNull(validated);
            Assert.Equal(operatorId, validated.OperatorId);
            Assert.Null(validated.CredentialId);
            Assert.Equal(kind, validated.Kind);
            Assert.Equal(role, validated.Role);
            Assert.Equal(securityStamp, validated.SecurityStampAtIssuance);
        }

        [Fact]
        public void Issue_WithPasswordKind_EmbeddedSecurityStamp()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            CredentialKind kind = CredentialKind.Password;
            OperatorRole role = OperatorRole.Review;

            // Act
            string token = service.Issue(operatorId, credentialId: null, kind, role, securityStamp);
            SessionPrincipal? validated = service.Validate(token);

            // Assert
            Assert.NotNull(validated);
            Assert.Equal(securityStamp, validated.SecurityStampAtIssuance);
            Assert.Equal(kind, validated.Kind);
            Assert.Null(validated.CredentialId);
        }

        [Fact]
        public void Issue_WithOperatorPasskeyKind_EmbeddedCredentialId()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var credentialId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            CredentialKind kind = CredentialKind.OperatorPasskey;
            OperatorRole role = OperatorRole.SuperAdmin;

            // Act
            string token = service.Issue(operatorId, credentialId, kind, role, securityStamp);
            SessionPrincipal? validated = service.Validate(token);

            // Assert
            Assert.NotNull(validated);
            Assert.Equal(credentialId, validated.CredentialId);
            Assert.Equal(kind, validated.Kind);
            Assert.Equal(operatorId, validated.OperatorId);
            Assert.Equal(securityStamp, validated.SecurityStampAtIssuance);
        }

        [Fact]
        public void Issue_TokenRoundTrip_IssueValidateSameFields()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var credentialId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            CredentialKind kind = CredentialKind.OperatorPasskey;
            OperatorRole role = OperatorRole.Admin;

            // Act
            string token = service.Issue(operatorId, credentialId, kind, role, securityStamp);
            SessionPrincipal? principal = service.Validate(token);

            // Assert
            Assert.NotNull(principal);
            Assert.Equal(operatorId, principal.OperatorId);
            Assert.Equal(credentialId, principal.CredentialId);
            Assert.Equal(kind, principal.Kind);
            Assert.Equal(role, principal.Role);
            Assert.Equal(securityStamp, principal.SecurityStampAtIssuance);
            Assert.NotEqual(default, principal.IssuedAtUtc);
            Assert.NotEqual(default, principal.ExpiresAtUtc);
            Assert.True(principal.ExpiresAtUtc > principal.IssuedAtUtc);
        }

        [Fact]
        public void Issue_WithDifferentRoles_RoleIsEmbedded()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            OperatorRole[] roles = new[] { OperatorRole.ReadOnly, OperatorRole.Review, OperatorRole.Admin, OperatorRole.SuperAdmin };

            // Act & Assert
            foreach (OperatorRole role in roles)
            {
                string token = service.Issue(operatorId, null, CredentialKind.Password, role, securityStamp);
                SessionPrincipal? principal = service.Validate(token);
                Assert.NotNull(principal);
                Assert.Equal(role, principal.Role);
            }
        }

        // ============ Validate() Tests ============

        [Fact]
        public void Validate_WithValidToken_DecryptsAndReturnsSessionPrincipal()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            string token = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);

            // Act
            SessionPrincipal? result = service.Validate(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(operatorId, result.OperatorId);
        }

        [Fact]
        public void Validate_WithTamperedToken_ReturnsNull()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            string token = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);

            // Act - flip a character to simulate tampering with the protected payload.
            char[] chars = token.ToCharArray();
            chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
            string tampered = new(chars);
            SessionPrincipal? result = service.Validate(tampered);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Validate_WithTamperedToken_DoesNotThrow()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            string token = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);

            // Act - flip a character and ensure no exception is thrown.
            char[] chars = token.ToCharArray();
            chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
            string tampered = new(chars);

            // Assert - should not throw, just return null
            Exception? exception = Record.Exception(() => service.Validate(tampered));
            Assert.Null(exception);
        }

        [Fact]
        public void Validate_WithMalformedToken_ReturnsNull()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);

            // Act
            SessionPrincipal? result = service.Validate("not-a-valid-token");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Validate_WithEmptyToken_ReturnsNull()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);

            // Act
            SessionPrincipal? result = service.Validate(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Validate_WithRecentlyExpiredToken_ReturnsNull()
        {
            // Arrange - create a token and artificially set its expiry to the past
            // by directly creating a SessionPrincipal with past expiry and protecting it
            var provider = new EphemeralDataProtectionProvider();
            IDataProtector protector = provider.CreateProtector("VideoForensics.SessionTokens.v1");
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            var expiredPrincipal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                securityStamp,
                DateTime.UtcNow.AddHours(-13),
                DateTime.UtcNow.AddHours(-1)  // Already expired
            );
            string json = System.Text.Json.JsonSerializer.Serialize(expiredPrincipal);
            string token = protector.Protect(json);
            var service = new SessionTokenService(provider);

            // Act
            SessionPrincipal? result = service.Validate(token);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Validate_WithFutureExpiryToken_ReturnsSessionPrincipal()
        {
            // Arrange - create a token with far-future expiry
            var provider = new EphemeralDataProtectionProvider();
            IDataProtector protector = provider.CreateProtector("VideoForensics.SessionTokens.v1");
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();
            var futurePrincipal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                securityStamp,
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow.AddHours(23)  // Expires in 23 hours
            );
            string json = System.Text.Json.JsonSerializer.Serialize(futurePrincipal);
            string token = protector.Protect(json);
            var service = new SessionTokenService(provider);

            // Act
            SessionPrincipal? result = service.Validate(token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(operatorId, result.OperatorId);
        }

        // ============ Security Tests ============

        [Fact]
        public void Validate_TokenProtectedAgainstTampering_WithDifferentProtector()
        {
            // Arrange
            var provider1 = new EphemeralDataProtectionProvider();
            var provider2 = new EphemeralDataProtectionProvider();
            var service1 = new SessionTokenService(provider1);
            var service2 = new SessionTokenService(provider2);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            string token = service1.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);

            // Act - try to validate with a different provider (simulating key rotation or different instance)
            SessionPrincipal? result = service2.Validate(token);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Issue_ConsecutiveCallsProduceDifferentTokens()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            // Act - issue two tokens in quick succession
            string token1 = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);
            System.Threading.Thread.Sleep(10); // Small delay to ensure different IssuedAtUtc
            string token2 = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);

            // Assert - tokens should be different because IssuedAtUtc differs
            Assert.NotEqual(token1, token2);
        }

        [Fact]
        public void Validate_SecurityStampMismatch_IsDetectedByComparison()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var initialSecurityStamp = Guid.NewGuid();
            var newSecurityStamp = Guid.NewGuid();

            string token = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, initialSecurityStamp);
            SessionPrincipal? principal = service.Validate(token);

            // Act - simulate password change (security stamp changes)
            Guid changedSecurityStamp = principal!.SecurityStampAtIssuance;

            // Assert - the principal carries the initial stamp; if we compare it against the new one, they differ
            Assert.Equal(initialSecurityStamp, changedSecurityStamp);
            Assert.NotEqual(newSecurityStamp, changedSecurityStamp);
        }

        [Fact]
        public void Issue_IssuedAtAndExpiresAtUtc_AreConsistent()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new SessionTokenService(provider);
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            // Act
            string token = service.Issue(operatorId, null, CredentialKind.Password, OperatorRole.Admin, securityStamp);
            SessionPrincipal? principal = service.Validate(token);

            // Assert
            Assert.NotNull(principal);
            // ExpiresAtUtc should be exactly 12 hours after IssuedAtUtc
            DateTime expectedExpiry = principal.IssuedAtUtc.AddHours(12);
            Assert.Equal(expectedExpiry, principal.ExpiresAtUtc);
            // ExpiresAtUtc should be in the future (not expired yet)
            Assert.True(principal.ExpiresAtUtc > DateTime.UtcNow);
        }

        [Fact]
        public void Validate_WithNullTokenJson_ReturnsNull()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            IDataProtector protector = provider.CreateProtector("VideoForensics.SessionTokens.v1");
            var service = new SessionTokenService(provider);

            // Create a token that deserializes to null (though this is edge case)
            // We'll just test that invalid JSON returns null gracefully
            string invalidJson = "not json at all";
            string token = protector.Protect(invalidJson);

            // Act
            SessionPrincipal? result = service.Validate(token);

            // Assert
            Assert.Null(result);
        }

        // ============ IssuePasskeySetupOnly() Tests ============

        [Fact]
        public void IssuePasskeySetupOnly_Validate_ReturnsLimitedPasswordPrincipal()
        {
            // Arrange
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            var operatorId = Guid.NewGuid();
            var securityStamp = Guid.NewGuid();

            // Act
            string token = service.IssuePasskeySetupOnly(operatorId, OperatorRole.Admin, securityStamp);
            SessionPrincipal? validated = service.Validate(token);

            // Assert
            Assert.NotNull(validated);
            Assert.True(validated.PasskeySetupOnly);
            Assert.Equal(CredentialKind.Password, validated.Kind);
            Assert.Equal(operatorId, validated.OperatorId);
            Assert.Equal(securityStamp, validated.SecurityStampAtIssuance);
            Assert.True(validated.IssuedAtUtc <= DateTime.UtcNow && validated.IssuedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public void IssuePasskeySetupOnly_Validate_LifetimeIsFifteenMinutes()
        {
            // Arrange
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());

            // Act
            SessionPrincipal? validated = service.Validate(service.IssuePasskeySetupOnly(Guid.NewGuid(), OperatorRole.Admin, Guid.NewGuid()));

            // Assert - the limited session must not outlive 15 minutes, unlike a normal 12-hour session
            Assert.NotNull(validated);
            Assert.True(validated.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(15));
            Assert.True(validated.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(14));
        }

        [Fact]
        public void Issue_NormalSession_ValidatesWithPasskeySetupOnlyFalse()
        {
            // Arrange - a normal token carries no limit, so every token issued before the field existed behaves the same
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());

            // Act
            SessionPrincipal? validated = service.Validate(service.Issue(Guid.NewGuid(), null, CredentialKind.Password, OperatorRole.Admin, Guid.NewGuid()));

            // Assert
            Assert.NotNull(validated);
            Assert.False(validated.PasskeySetupOnly);
        }
    }
}
