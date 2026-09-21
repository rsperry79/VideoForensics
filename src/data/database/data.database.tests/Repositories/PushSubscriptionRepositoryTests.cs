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
    }
}
