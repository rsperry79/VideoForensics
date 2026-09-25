using Microsoft.AspNetCore.DataProtection;

using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class SessionTierHeaderProtectorTests
    {
        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

            public void SetUtcNow(DateTime utcNow)
            {
                _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
            }

            public override DateTimeOffset GetUtcNow()
            {
                return _utcNow;
            }
        }

        [Fact]
        public void Protect_ThenTryUnprotect_RoundTripsTierAndOperatorId()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var protector = new SessionTierHeaderProtector(provider);
            var operatorId = Guid.NewGuid();

            // Act
            string header = protector.Protect(NetworkTier.Local, operatorId);
            bool ok = protector.TryUnprotect(header, out NetworkTier tier, out Guid resolvedOperatorId);

            // Assert
            Assert.True(ok);
            Assert.Equal(NetworkTier.Local, tier);
            Assert.Equal(operatorId, resolvedOperatorId);
        }

        [Fact]
        public void TryUnprotect_TamperedHeader_ReturnsFalse()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var protector = new SessionTierHeaderProtector(provider);
            string header = protector.Protect(NetworkTier.Local, Guid.NewGuid());

            // Act - flip a character to simulate tampering with the protected payload.
            char[] chars = header.ToCharArray();
            chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
            string tampered = new string(chars);
            bool ok = protector.TryUnprotect(tampered, out NetworkTier tier, out Guid operatorId);

            // Assert
            Assert.False(ok);
            Assert.Equal(default, tier);
            Assert.Equal(default, operatorId);
        }

        [Fact]
        public void TryUnprotect_ExpiredHeader_ReturnsFalse()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var timeProvider = new FakeTimeProvider();
            timeProvider.SetUtcNow(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var protector = new SessionTierHeaderProtector(provider, timeProvider);
            var operatorId = Guid.NewGuid();

            string header = protector.Protect(NetworkTier.Local, operatorId);

            // Act - advance beyond the header's short (<=2 minute) lifetime.
            timeProvider.SetUtcNow(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc));
            bool ok = protector.TryUnprotect(header, out NetworkTier tier, out Guid resolvedOperatorId);

            // Assert
            Assert.False(ok);
        }

        [Fact]
        public void TryUnprotect_EmptyOrNullHeader_ReturnsFalse()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var protector = new SessionTierHeaderProtector(provider);

            // Act & Assert
            Assert.False(protector.TryUnprotect(string.Empty, out _, out _));
        }
    }
}
