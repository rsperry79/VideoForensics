using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// An event waiting in the backfill queue keeps its attempt count, its next retry time and its unavailable status across a save.
    /// </summary>
    public class EventRetryStateTests : IAsyncLifetime
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
        public async Task Event_SavedWithRetryState_ReadsBackAttemptsNextAttemptAndUnavailableStatus()
        {
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: "86401464");
            var nextAttempt = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
            var evt = new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                ProviderEventId = "86401464",
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow,
                DownloadStatus = EventDownloadStatus.Unavailable,
                DownloadAttempts = 3,
                NextAttemptAtUtc = nextAttempt,
            };
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Devices.Add(device);
                _ = seed.Events.Add(evt);
                _ = await seed.SaveChangesAsync();
            }

            await using VideoForensicsDbContext check = _fixture.Factory.CreateDbContext();
            Event saved = await check.Events.AsNoTracking().SingleAsync(e => e.Id == evt.Id);

            Assert.Equal(EventDownloadStatus.Unavailable, saved.DownloadStatus);
            Assert.Equal(3, saved.DownloadAttempts);
            Assert.Equal(nextAttempt, saved.NextAttemptAtUtc);
        }

        [Fact]
        public async Task ListAwaitingDingId_ReturnsOnlyQueuedEventsOfTheDeviceUnderTheAttemptCap()
        {
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: "86401464");
            Device other = TestDataBuilder.BuildDevice(providerDeviceId: "86401465");
            Event Make(Guid deviceId, bool awaiting, int attempts) => new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = Guid.NewGuid().ToString("N"),
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow,
                AwaitingDingId = awaiting,
                DownloadAttempts = attempts,
            };
            Event queued = Make(device.Id, awaiting: true, attempts: 0);
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Devices.Add(device);
                _ = seed.Devices.Add(other);
                seed.Events.AddRange(queued, Make(device.Id, awaiting: true, attempts: 3), Make(device.Id, awaiting: false, attempts: 0), Make(other.Id, awaiting: true, attempts: 0));
                _ = await seed.SaveChangesAsync();
            }

            var repository = new EventRepository(_fixture.Factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<EventRepository>.Instance);
            IReadOnlyList<Event> result = await repository.ListAwaitingDingIdAsync(device.Id, 3, CancellationToken.None);

            Assert.Equal(new[] { queued.Id }, result.Select(e => e.Id));
        }
    }
}
