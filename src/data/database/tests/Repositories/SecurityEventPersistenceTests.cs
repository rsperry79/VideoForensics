using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Persistence tests for SecurityEvent rows written for login attempts against an unknown username,
    /// which have no operator and carry the typed username instead.
    /// </summary>
    public class SecurityEventPersistenceTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task SecurityEventPersistenceTests_NullOperatorWithAttemptedUsername_RoundTrips()
        {
            // Arrange
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                OperatorId = null,
                EventType = SecurityEventType.LoginAttemptFailure,
                Success = false,
                IpAddress = "192.168.1.50",
                OccurredAtUtc = DateTime.UtcNow,
                Reason = "Invalid username",
                AttemptedUsername = "zumba",
                CreatedAtUtc = DateTime.UtcNow
            };

            await using (VideoForensicsDbContext writer = await _fixture.Factory.CreateDbContextAsync())
            {
                _ = writer.SecurityEvents.Add(securityEvent);
                _ = await writer.SaveChangesAsync();
            }

            // Act
            await using VideoForensicsDbContext reader = await _fixture.Factory.CreateDbContextAsync();
            SecurityEvent? stored = await reader.SecurityEvents.SingleOrDefaultAsync(e => e.Id == securityEvent.Id);

            // Assert
            Assert.NotNull(stored);
            Assert.Null(stored.OperatorId);
            Assert.Equal("zumba", stored.AttemptedUsername);
            Assert.Equal("Invalid username", stored.Reason);
        }
    }
}
