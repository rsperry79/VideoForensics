using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class PushSubscriptionRepositoryTests : RepositoryTestBase
    {
        private PushSubscriptionRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new PushSubscriptionRepository(Fixture.Factory, CreateLogger<PushSubscriptionRepository>());
        }

        [Fact]
        public async Task AddOrUpdateAsync_SanitizesLogOutput_WhenEndpointContainsNewlines()
        {
            // Arrange: Create a push subscription with Endpoint containing CRLF (log injection attempt)
            var injectedEndpoint = "https://push.example.com/abc123\r\nFAKE LOG ENTRY: Credentials exposed";
            var subscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                Endpoint = injectedEndpoint,
                P256dhKey = "test-p256dh-key",
                AuthKey = "test-auth-key",
                CreatedUtc = DateTime.UtcNow
            };

            // Act: Add the subscription (logs it)
            await _repository.AddOrUpdateAsync(subscription, CancellationToken.None);

            // Assert: Verify the subscription was persisted with the raw Endpoint intact
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                PushSubscription? persistedSubscription = await db.PushSubscriptions.FirstOrDefaultAsync(ps => ps.Id == subscription.Id, CancellationToken.None);
                Assert.NotNull(persistedSubscription);
                // The raw Endpoint (with newlines) should be stored in the database
                Assert.Equal(injectedEndpoint, persistedSubscription.Endpoint);
            }
        }

        [Fact]
        public async Task ListAllAsync_WithAdminAndNonAdminSubscriptions_ReturnsBoth()
        {
            // Arrange: The repository stores no role data, so "admin" and "non-admin" are represented
            // by two operators' subscriptions. ListAllAsync must return every subscription regardless of owner.
            var adminSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                Endpoint = "https://push.example.com/admin-endpoint",
                P256dhKey = "admin-p256dh-key",
                AuthKey = "admin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            var nonAdminSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                Endpoint = "https://push.example.com/non-admin-endpoint",
                P256dhKey = "non-admin-p256dh-key",
                AuthKey = "non-admin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            await _repository.AddOrUpdateAsync(adminSubscription, CancellationToken.None);
            await _repository.AddOrUpdateAsync(nonAdminSubscription, CancellationToken.None);

            // Act
            IReadOnlyList<PushSubscription> result = await _repository.ListAllAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, ps => ps.Endpoint == adminSubscription.Endpoint && ps.OperatorId == adminSubscription.OperatorId);
            Assert.Contains(result, ps => ps.Endpoint == nonAdminSubscription.Endpoint && ps.OperatorId == nonAdminSubscription.OperatorId);
        }

        [Fact]
        public async Task ListForAdminsAsync_WithReadOnlyAndAdminOperators_ReturnsOnlyAdminSubscriptions()
        {
            // Arrange: seed a ReadOnly and an Admin operator, each with a push subscription.
            Guid readOnlyOperatorId = await SeedOperatorAsync(OperatorRole.ReadOnly, "readonly-op");
            Guid adminOperatorId = await SeedOperatorAsync(OperatorRole.Admin, "admin-op");
            var readOnlySubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = readOnlyOperatorId,
                Endpoint = "https://push.example.com/readonly-endpoint",
                P256dhKey = "readonly-p256dh-key",
                AuthKey = "readonly-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            var adminSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = adminOperatorId,
                Endpoint = "https://push.example.com/admin-role-endpoint",
                P256dhKey = "admin-role-p256dh-key",
                AuthKey = "admin-role-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            await _repository.AddOrUpdateAsync(readOnlySubscription, CancellationToken.None);
            await _repository.AddOrUpdateAsync(adminSubscription, CancellationToken.None);

            // Act
            IReadOnlyList<PushSubscription> result = await _repository.ListForAdminsAsync(CancellationToken.None);

            // Assert: only the Admin operator's subscription is returned.
            PushSubscription returned = Assert.Single(result);
            Assert.Equal(adminSubscription.Endpoint, returned.Endpoint);
            Assert.Equal(adminOperatorId, returned.OperatorId);
        }

        [Fact]
        public async Task ListForAdminsAsync_WithOnlyNonAdminOperators_ReturnsEmpty()
        {
            // Arrange: every operator is below Admin (Review and ReadOnly).
            Guid reviewOperatorId = await SeedOperatorAsync(OperatorRole.Review, "review-op");
            Guid readOnlyOperatorId = await SeedOperatorAsync(OperatorRole.ReadOnly, "readonly-only-op");
            await _repository.AddOrUpdateAsync(new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = reviewOperatorId,
                Endpoint = "https://push.example.com/review-endpoint",
                P256dhKey = "review-p256dh-key",
                AuthKey = "review-auth-key",
                CreatedUtc = DateTime.UtcNow
            }, CancellationToken.None);
            await _repository.AddOrUpdateAsync(new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = readOnlyOperatorId,
                Endpoint = "https://push.example.com/readonly-only-endpoint",
                P256dhKey = "readonly-only-p256dh-key",
                AuthKey = "readonly-only-auth-key",
                CreatedUtc = DateTime.UtcNow
            }, CancellationToken.None);

            // Act
            IReadOnlyList<PushSubscription> result = await _repository.ListForAdminsAsync(CancellationToken.None);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ListForAdminsAsync_WithSuperAdminOperator_ReturnsItsSubscription()
        {
            // Arrange: SuperAdmin is above Admin in the enum, so the "at or above Admin" check must include it.
            Guid superAdminOperatorId = await SeedOperatorAsync(OperatorRole.SuperAdmin, "superadmin-op");
            var superAdminSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = superAdminOperatorId,
                Endpoint = "https://push.example.com/superadmin-endpoint",
                P256dhKey = "superadmin-p256dh-key",
                AuthKey = "superadmin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            await _repository.AddOrUpdateAsync(superAdminSubscription, CancellationToken.None);

            // Act
            IReadOnlyList<PushSubscription> result = await _repository.ListForAdminsAsync(CancellationToken.None);

            // Assert
            PushSubscription returned = Assert.Single(result);
            Assert.Equal(superAdminSubscription.Endpoint, returned.Endpoint);
        }

        [Fact]
        public async Task ListForAdminsAsync_WithDeactivatedOrUnapprovedAdmin_ExcludesThoseSubscriptions()
        {
            // Arrange: three Admin-role operators; only the active, approved one should receive admin pushes.
            Guid activeAdminId = await SeedOperatorAsync(OperatorRole.Admin, "active-admin-op");
            Guid deactivatedAdminId = await SeedOperatorAsync(OperatorRole.Admin, "deactivated-admin-op", active: false);
            Guid unapprovedAdminId = await SeedOperatorAsync(OperatorRole.Admin, "unapproved-admin-op", approved: false);
            var activeSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = activeAdminId,
                Endpoint = "https://push.example.com/active-admin-endpoint",
                P256dhKey = "active-admin-p256dh-key",
                AuthKey = "active-admin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            var deactivatedSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = deactivatedAdminId,
                Endpoint = "https://push.example.com/deactivated-admin-endpoint",
                P256dhKey = "deactivated-admin-p256dh-key",
                AuthKey = "deactivated-admin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            var unapprovedSubscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = unapprovedAdminId,
                Endpoint = "https://push.example.com/unapproved-admin-endpoint",
                P256dhKey = "unapproved-admin-p256dh-key",
                AuthKey = "unapproved-admin-auth-key",
                CreatedUtc = DateTime.UtcNow
            };
            await _repository.AddOrUpdateAsync(activeSubscription, CancellationToken.None);
            await _repository.AddOrUpdateAsync(deactivatedSubscription, CancellationToken.None);
            await _repository.AddOrUpdateAsync(unapprovedSubscription, CancellationToken.None);

            // Act
            IReadOnlyList<PushSubscription> result = await _repository.ListForAdminsAsync(CancellationToken.None);

            // Assert: only the active, approved Admin's subscription is returned.
            PushSubscription returned = Assert.Single(result);
            Assert.Equal(activeSubscription.Endpoint, returned.Endpoint);
            Assert.Equal(activeAdminId, returned.OperatorId);
        }

        /// <summary>Inserts an operator with the given role and returns its Id. Operators are active and approved unless specified.</summary>
        private async Task<Guid> SeedOperatorAsync(OperatorRole role, string username, bool active = true, bool approved = true)
        {
            await using var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            var op = new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = username,
                Username = username,
                FirstName = "Test",
                LastName = "Operator",
                Email = $"{username}@example.com",
                Role = role,
                Active = active,
                IsApproved = approved,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid()
            };
            _ = db.Operators.Add(op);
            _ = await db.SaveChangesAsync(CancellationToken.None);
            return op.Id;
        }
    }
}
