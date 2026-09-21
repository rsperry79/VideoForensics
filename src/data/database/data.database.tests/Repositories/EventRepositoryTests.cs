using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class EventRepositoryTests : RepositoryTestBase
    {
        private EventRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new EventRepository(Fixture.Factory, CreateLogger<EventRepository>());
        }

        [Fact]
        public async Task UpsertAsync_SanitizesLogOutput_WhenProviderEventIdContainsNewlines()
        {
            // Arrange: Create an event with a ProviderEventId containing CRLF (log injection attempt)
            var deviceId = Guid.NewGuid();
            var injectedEventId = "event123\r\nFAKE LOG ENTRY: Unauthorized access";
            var @event = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = injectedEventId,
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow,
                DownloadStatus = EventDownloadStatus.NotAttempted
            };

            // Act: Upsert the event (logs it)
            await _repository.UpsertAsync(@event, CancellationToken.None);

            // Assert: Verify the event was persisted with the raw ProviderEventId intact
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                Event? persistedEvent = await db.Events.FindAsync(new object[] { @event.Id }, cancellationToken: CancellationToken.None);
                Assert.NotNull(persistedEvent);
                // The raw ProviderEventId (with newlines) should be stored in the database
                Assert.Equal(injectedEventId, persistedEvent.ProviderEventId);
            }
        }
    }
}
