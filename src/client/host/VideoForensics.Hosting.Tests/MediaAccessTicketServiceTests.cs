using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class MediaAccessTicketServiceTests
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

            public override long GetTimestamp()
            {
                return _utcNow.UtcTicks;
            }
        }

        [Fact]
        public void IssueAndValidate_RoundTrip_ReturnsOperatorId()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider);
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Act
            var ticket = service.Issue(mediaItemId, operatorId);
            var validatedOperatorId = service.Validate(ticket.Token, mediaItemId);

            // Assert
            Assert.Equal(operatorId, validatedOperatorId);
        }

        [Fact]
        public void Validate_WithWrongMediaId_ReturnsNull()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider);
            var mediaItemId = Guid.NewGuid();
            var wrongMediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Act
            var ticket = service.Issue(mediaItemId, operatorId);
            var validatedOperatorId = service.Validate(ticket.Token, wrongMediaItemId);

            // Assert
            Assert.Null(validatedOperatorId);
        }

        [Fact]
        public void Validate_WithExpiredTicket_ReturnsNull()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider, fakeTime);
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Act
            var ticket = service.Issue(mediaItemId, operatorId);
            fakeTime.SetUtcNow(fakeTime.GetUtcNow().UtcDateTime.AddMinutes(11)); // Advance past 10-minute lifetime
            var validatedOperatorId = service.Validate(ticket.Token, mediaItemId);

            // Assert
            Assert.Null(validatedOperatorId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid-garbage-token")]
        public void Validate_WithNullEmptyOrGarbageToken_ReturnsNull(string? token)
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider);
            var mediaItemId = Guid.NewGuid();

            // Act
            var validatedOperatorId = service.Validate(token, mediaItemId);

            // Assert
            Assert.Null(validatedOperatorId);
        }

        [Fact]
        public void Issue_CreatesTicketWithExpiryAtUtcNowPlus10Minutes()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            var initialTime = DateTime.UtcNow;
            fakeTime.SetUtcNow(initialTime);
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider, fakeTime);
            var mediaItemId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();

            // Act
            var ticket = service.Issue(mediaItemId, operatorId);

            // Assert
            Assert.Equal(initialTime.AddMinutes(10), ticket.ExpiresAtUtc, TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void TicketLifetime_Returns10Minutes()
        {
            // Arrange
            var provider = new EphemeralDataProtectionProvider();
            var service = new MediaAccessTicketService(provider);

            // Act & Assert
            Assert.Equal(TimeSpan.FromMinutes(10), service.TicketLifetime);
        }
    }
}
