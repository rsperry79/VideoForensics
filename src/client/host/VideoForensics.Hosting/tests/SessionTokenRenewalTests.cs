using Microsoft.AspNetCore.DataProtection;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests
{
    public class SessionTokenRenewalTests
    {
        private static SessionPrincipal Principal(DateTime issuedUtc, DateTime expiresUtc, bool passkeySetupOnly = false)
        {
            return new SessionPrincipal(Guid.NewGuid(), Guid.NewGuid(), CredentialKind.OperatorPasskey, OperatorRole.Admin, Guid.NewGuid(), issuedUtc, expiresUtc)
            {
                PasskeySetupOnly = passkeySetupOnly
            };
        }

        [Fact]
        public void Renew_PastHalfLife_ReturnsTokenWithSameIdentityAndLaterExpiry()
        {
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            DateTime issued = DateTime.UtcNow;
            SessionPrincipal current = Principal(issued, issued.AddHours(12));
            DateTime now = issued.AddHours(7);

            string? renewed = service.Renew(current, now);

            Assert.NotNull(renewed);
            SessionPrincipal? validated = service.Validate(renewed!);
            Assert.NotNull(validated);
            Assert.Equal(current.OperatorId, validated!.OperatorId);
            Assert.Equal(current.CredentialId, validated.CredentialId);
            Assert.Equal(current.Kind, validated.Kind);
            Assert.Equal(current.Role, validated.Role);
            Assert.Equal(current.SecurityStampAtIssuance, validated.SecurityStampAtIssuance);
            Assert.True(validated.ExpiresAtUtc > current.ExpiresAtUtc, "A renewed token must expire later than the one it replaces.");
        }

        [Fact]
        public void Renew_BeforeHalfLife_ReturnsNull()
        {
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            DateTime issued = DateTime.UtcNow;
            SessionPrincipal current = Principal(issued, issued.AddHours(12));

            Assert.Null(service.Renew(current, issued.AddHours(3)));
        }

        [Fact]
        public void Renew_PasskeySetupOnlySession_ReturnsNull()
        {
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            DateTime issued = DateTime.UtcNow;
            SessionPrincipal current = Principal(issued, issued.AddMinutes(15), passkeySetupOnly: true);

            Assert.Null(service.Renew(current, issued.AddMinutes(10)));
        }

        [Fact]
        public void Renew_PastAbsoluteCap_ReturnsNull()
        {
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            DateTime issued = DateTime.UtcNow;
            SessionPrincipal current = Principal(issued, issued.AddHours(12));

            Assert.Null(service.Renew(current, issued.AddHours(24)));
        }

        [Fact]
        public void Renew_NeverExtendsPastAbsoluteCap()
        {
            var service = new SessionTokenService(new EphemeralDataProtectionProvider());
            DateTime issued = DateTime.UtcNow;
            // Renewed earlier, so the session is still live at +20h with less than half its lifetime left.
            SessionPrincipal current = Principal(issued, issued.AddHours(23));

            string? renewed = service.Renew(current, issued.AddHours(20));

            Assert.NotNull(renewed);
            SessionPrincipal? validated = service.Validate(renewed!);
            Assert.NotNull(validated);
            Assert.True(validated!.ExpiresAtUtc <= issued.AddHours(24), "A renewed token must not outlive the 24-hour absolute cap.");
        }
    }
}
