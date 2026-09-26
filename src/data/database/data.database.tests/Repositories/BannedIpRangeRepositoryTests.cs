using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class BannedIpRangeRepositoryTests : RepositoryTestBase
    {
        private BannedIpRangeRepository _repository = null!;
        private Guid _testOperatorId = Guid.NewGuid();

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new BannedIpRangeRepository(Fixture.Factory, CreateLogger<BannedIpRangeRepository>());
            
            // Create a test operator for FK
            await using (var db = await Fixture.Factory.CreateDbContextAsync())
            {
                db.Operators.Add(new Operator
                {
                    Id = _testOperatorId,
                    DisplayName = "Test Admin",
                    Username = "testadmin",
                    Email = "admin@example.com",
                    FirstName = "Test",
                    LastName = "Admin",
                    CreatedAtUtc = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid(),
                    Role = OperatorRole.SuperAdmin,
                    Active = true,
                    IsApproved = true
                });
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task GetActiveAsync_WhenEmpty_ReturnsEmptyList()
        {
            // Act: Get active bans when none exist
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);

            // Assert: Should return empty list
            Assert.NotNull(activeBans);
            Assert.Empty(activeBans);
        }

        [Fact]
        public async Task AddAsync_AddsNewBan()
        {
            // Arrange: Create a permanent ban (ExpiresAtUtc = null)
            var ban = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "203.0.113.0/24",
                Reason = "Malicious activity",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = null
            };

            // Act: Add the ban
            await _repository.AddAsync(ban, CancellationToken.None);

            // Assert: Verify it appears in active bans
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);
            Assert.Single(activeBans);
            Assert.Equal("203.0.113.0/24", activeBans[0].CidrRange);
            Assert.Equal("Malicious activity", activeBans[0].Reason);
        }

        [Fact]
        public async Task GetActiveAsync_ExcludesExpiredBans()
        {
            // Arrange: Add a permanent ban and an expired ban
            var permanentBan = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "203.0.113.0/24",
                Reason = "Permanent",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = null
            };
            
            var expiredBan = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "192.0.2.0/24",
                Reason = "Expired",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-7),
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(-1) // Already expired
            };
            
            await _repository.AddAsync(permanentBan, CancellationToken.None);
            await _repository.AddAsync(expiredBan, CancellationToken.None);

            // Act: Get active bans
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);

            // Assert: Should only return the permanent ban
            Assert.Single(activeBans);
            Assert.Equal("203.0.113.0/24", activeBans[0].CidrRange);
        }

        [Fact]
        public async Task GetActiveAsync_IncludesFutureExpiringBans()
        {
            // Arrange: Add a ban that expires in the future
            var futureBan = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "198.51.100.0/24",
                Reason = "Temporary block",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1) // Expires tomorrow
            };

            // Act: Add and retrieve
            await _repository.AddAsync(futureBan, CancellationToken.None);
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);

            // Assert: Should include the future-expiring ban
            Assert.Single(activeBans);
            Assert.Equal("198.51.100.0/24", activeBans[0].CidrRange);
        }

        [Fact]
        public async Task RemoveAsync_DeletesExistingBan()
        {
            // Arrange: Add a ban
            var ban = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "203.0.113.0/24",
                Reason = "To be removed",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = null
            };
            await _repository.AddAsync(ban, CancellationToken.None);

            // Act: Remove the ban
            await _repository.RemoveAsync(ban.Id, CancellationToken.None);

            // Assert: Should no longer appear in active bans
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);
            Assert.Empty(activeBans);
        }

        [Fact]
        public async Task RemoveAsync_WhenNotExists_DoesNotThrow()
        {
            // Act & Assert: Should not throw when removing non-existent ban
            await _repository.RemoveAsync(Guid.NewGuid(), CancellationToken.None);
        }

        [Fact]
        public async Task RemoveAsync_OnlyRemovesSpecifiedBan()
        {
            // Arrange: Add two bans
            var ban1 = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "203.0.113.0/24",
                Reason = "Ban 1",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = null
            };
            
            var ban2 = new BannedIpRange
            {
                Id = Guid.NewGuid(),
                CidrRange = "192.0.2.0/24",
                Reason = "Ban 2",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByOperatorId = _testOperatorId,
                ExpiresAtUtc = null
            };
            
            await _repository.AddAsync(ban1, CancellationToken.None);
            await _repository.AddAsync(ban2, CancellationToken.None);

            // Act: Remove only ban1
            await _repository.RemoveAsync(ban1.Id, CancellationToken.None);

            // Assert: ban2 should still exist
            IReadOnlyList<BannedIpRange> activeBans = await _repository.GetActiveAsync(CancellationToken.None);
            Assert.Single(activeBans);
            Assert.Equal("192.0.2.0/24", activeBans[0].CidrRange);
        }
    }
}
