using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.ReferenceData;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Verifies the EventTypeLinks backfill on a real SQLite database: each rule derives the right links, and a rerun inserts nothing.
    /// The new tables are created with raw DDL because the test fixture builds the schema from the DbSets it knows about.
    /// </summary>
    public class EventTypeBackfillTests : IAsyncLifetime
    {
        private const string Ring = "ring";

        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();

            // Events reference Devices, which these tests do not need. Foreign keys are off for this connection only.
            _ = await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");

            _ = await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE EventTypeLinks (
                    EventId TEXT NOT NULL,
                    EventTypeCode TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (EventId, EventTypeCode))
                """);

            _ = await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE ProviderEventTypeMappings (
                    ProviderKey TEXT NOT NULL,
                    ProviderValue TEXT NOT NULL,
                    EventTypeCode TEXT NOT NULL,
                    PRIMARY KEY (ProviderKey, ProviderValue))
                """);

            _ = await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO ProviderEventTypeMappings (ProviderKey, ProviderValue, EventTypeCode) VALUES
                    ('ring', 'human', 'person'),
                    ('ring', 'animal', 'animal'),
                    ('ring', 'vehicle', 'vehicle'),
                    ('ring', 'other_motion', 'motion')
                """);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        /// <summary>Adds one event with an optional CV detection and an optional occurrence on that detection.</summary>
        private async Task<Guid> AddEventAsync(string rawKind, string? cvType = null, string? occurrenceType = null)
        {
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            var eventId = Guid.NewGuid();
            var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

            _ = db.Events.Add(new Event
            {
                Id = eventId,
                DeviceId = Guid.NewGuid(),
                ProviderEventId = Guid.NewGuid().ToString(),
                EventType = rawKind,
                OccurredAtUtc = now,
                DiscoveredAtUtc = now,
            });

            if (cvType is not null)
            {
                var detectionId = Guid.NewGuid();
                _ = db.EventDetections.Add(new EventDetection { Id = detectionId, EventId = eventId, DetectionType = cvType });

                if (occurrenceType is not null)
                {
                    _ = db.EventDetectionTypeOccurrences.Add(new EventDetectionTypeOccurrence
                    {
                        Id = Guid.NewGuid(),
                        EventDetectionId = detectionId,
                        DetectionType = occurrenceType,
                        DetectedAtUtc = now,
                    });
                }
            }

            _ = await db.SaveChangesAsync();
            return eventId;
        }

        private async Task<List<string>> LinkCodesAsync(Guid eventId)
        {
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            return await db.Database.SqlQuery<string>(
                    $"SELECT EventTypeCode AS Value FROM EventTypeLinks WHERE EventId = {eventId} ORDER BY EventTypeCode")
                .ToListAsync();
        }

        [Fact]
        public async Task RunAsync_PersonDetectionOnDingEvent_LinksPersonOnly()
        {
            Guid eventId = await AddEventAsync("ding", cvType: "human");

            _ = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(["person"], await LinkCodesAsync(eventId));
        }

        [Fact]
        public async Task RunAsync_MotionEventWithoutCv_LinksMotion()
        {
            Guid eventId = await AddEventAsync("motion");

            _ = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(["motion"], await LinkCodesAsync(eventId));
        }

        [Fact]
        public async Task RunAsync_MotionEventWithPersonDetection_LinksPersonWithoutMotion()
        {
            Guid eventId = await AddEventAsync("motion", cvType: "human");

            _ = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(["person"], await LinkCodesAsync(eventId));
        }

        [Fact]
        public async Task RunAsync_OtherMotionDetection_LinksMotion()
        {
            Guid eventId = await AddEventAsync("ding", cvType: "other_motion");

            _ = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(["motion"], await LinkCodesAsync(eventId));
        }

        [Fact]
        public async Task RunAsync_AnimalOnlyInOccurrenceRows_LinksAnimal()
        {
            Guid eventId = await AddEventAsync("motion", cvType: null, occurrenceType: null);
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                var detectionId = Guid.NewGuid();
                var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
                _ = db.EventDetections.Add(new EventDetection { Id = detectionId, EventId = eventId, DetectionType = null });
                _ = db.EventDetectionTypeOccurrences.Add(new EventDetectionTypeOccurrence
                {
                    Id = Guid.NewGuid(),
                    EventDetectionId = detectionId,
                    DetectionType = "animal",
                    DetectedAtUtc = now,
                });
                _ = await db.SaveChangesAsync();
            }

            _ = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(["animal"], await LinkCodesAsync(eventId));
        }

        [Fact]
        public async Task RunAsync_CalledTwice_SecondRunInsertsNothing()
        {
            _ = await AddEventAsync("motion");
            _ = await AddEventAsync("ding", cvType: "human");

            int first = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);
            int second = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(2, first);
            Assert.Equal(0, second);
        }

        [Fact]
        public async Task RunAsync_MoreThanOneBatch_LinksEveryEvent()
        {
            // BatchSize is 1000, so 1001 events exercise a second keyset page.
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync())
            {
                var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
                for (int i = 0; i < 1001; i++)
                {
                    _ = db.Events.Add(new Event
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        ProviderEventId = $"batch-{i}",
                        EventType = "motion",
                        OccurredAtUtc = now,
                        DiscoveredAtUtc = now,
                    });
                }

                _ = await db.SaveChangesAsync();
            }

            int inserted = await EventTypeBackfill.RunAsync(_fixture.Factory, Ring, CancellationToken.None);

            Assert.Equal(1001, inserted);
        }
    }
}
