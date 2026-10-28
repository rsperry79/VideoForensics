using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Tests for the event detail read path behind the review page's Detail tab: one batched read that returns the
    /// detection and its child rows for each requested event, and omits events that have no detail rows.
    /// </summary>
    public class EventDetailRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private EventDetailRepository _repository = null!;

        private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new EventDetailRepository(_fixture.Factory, NullLogger<EventDetailRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        /// <summary>Writes an event with a full set of detail rows: detection, two occurrences (inserted out of order), a zone, a person and an alert.</summary>
        private async Task<Guid> SeedEventWithDetailsAsync(string tag)
        {
            Guid eventId = Guid.NewGuid();
            Guid detectionId = Guid.NewGuid();

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            _ = db.Events.Add(new Event
            {
                Id = eventId,
                DeviceId = Guid.NewGuid(),
                ProviderEventId = $"prov-{tag}-{eventId:N}",
                EventType = "motion",
                OccurredAtUtc = Start,
                DiscoveredAtUtc = Start,
            });
            _ = db.EventDetections.Add(new EventDetection
            {
                Id = detectionId,
                EventId = eventId,
                PersonDetected = true,
                StreamBroken = false,
                DetectionType = $"Motion-{tag}",
                FullDescription = $"full-{tag}",
                ShortDescription = $"short-{tag}",
                Similarity = 0.5m,
                Anomaly = 0.1m,
                Confidence = 0.9m,
                ModelVersion = "v1",
            });
            _ = db.EventDetectionTypeOccurrences.Add(new EventDetectionTypeOccurrence
            {
                Id = Guid.NewGuid(),
                EventDetectionId = detectionId,
                DetectionType = "Person",
                DetectedAtUtc = Start.AddSeconds(30),
            });
            _ = db.EventDetectionTypeOccurrences.Add(new EventDetectionTypeOccurrence
            {
                Id = Guid.NewGuid(),
                EventDetectionId = detectionId,
                DetectionType = "Motion",
                DetectedAtUtc = Start.AddSeconds(10),
            });
            _ = db.EventDetectionZones.Add(new EventDetectionZone
            {
                Id = Guid.NewGuid(),
                EventDetectionId = detectionId,
                ZoneId = $"zone-{tag}",
                ZoneName = "Driveway",
                Confidence = 0.8m,
            });
            _ = db.EventDetectedPersons.Add(new EventDetectedPerson
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                ProfileId = $"profile-{tag}",
                ProfileName = "Alex",
                Confidence = 0.7m,
                ThumbnailUrl = "https://example.test/t.jpg",
            });
            _ = db.EventSecurityAlerts.Add(new EventSecurityAlert
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Severity = "High",
                AlertText = $"alert-{tag}",
            });
            _ = await db.SaveChangesAsync();
            return eventId;
        }

        /// <summary>Verifies that one event's detection and every child row come back, with occurrences in time order.</summary>
        [Fact]
        public async Task EventDetailRepository_GetDetailsAsync_EventWithChildren_ReturnsAllRowsWithOccurrencesOrdered()
        {
            Guid eventId = await SeedEventWithDetailsAsync("one");

            IReadOnlyList<EventDetail> details = await _repository.GetDetailsAsync(new[] { eventId }, CancellationToken.None);

            EventDetail detail = Assert.Single(details);
            Assert.Equal(eventId, detail.EventId);
            Assert.NotNull(detail.Detection);
            Assert.Equal("Motion-one", detail.Detection!.DetectionType);
            Assert.True(detail.Detection.PersonDetected);
            Assert.Equal("v1", detail.Detection.ModelVersion);
            Assert.Equal(new[] { "Motion", "Person" }, detail.Occurrences.Select(o => o.DetectionType).ToArray());
            Assert.Equal(new[] { Start.AddSeconds(10), Start.AddSeconds(30) }, detail.Occurrences.Select(o => o.DetectedAtUtc).ToArray());
            Assert.Equal("zone-one", Assert.Single(detail.Zones).ZoneId);
            Assert.Equal("profile-one", Assert.Single(detail.People).ProfileId);
            Assert.Equal("alert-one", Assert.Single(detail.SecurityAlerts).AlertText);
        }

        /// <summary>Verifies that a requested event with no detail rows is left out of the result instead of returning an empty entry.</summary>
        [Fact]
        public async Task EventDetailRepository_GetDetailsAsync_EventWithoutRows_IsOmitted()
        {
            Guid withRows = await SeedEventWithDetailsAsync("present");
            Guid withoutRows = Guid.NewGuid();

            IReadOnlyList<EventDetail> details = await _repository.GetDetailsAsync(new[] { withRows, withoutRows }, CancellationToken.None);

            EventDetail detail = Assert.Single(details);
            Assert.Equal(withRows, detail.EventId);
        }

        /// <summary>Verifies that an empty id set returns an empty list.</summary>
        [Fact]
        public async Task EventDetailRepository_GetDetailsAsync_EmptyIds_ReturnsEmpty()
        {
            IReadOnlyList<EventDetail> details = await _repository.GetDetailsAsync(Array.Empty<Guid>(), CancellationToken.None);

            Assert.Empty(details);
        }

        /// <summary>Verifies that more than 200 ids is rejected, bounding the size of the IN list.</summary>
        [Fact]
        public async Task EventDetailRepository_GetDetailsAsync_TooManyIds_ThrowsArgumentOutOfRange()
        {
            Guid[] ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.GetDetailsAsync(ids, CancellationToken.None));
        }

        /// <summary>Verifies that asking for one event never returns another event's child rows.</summary>
        [Fact]
        public async Task EventDetailRepository_GetDetailsAsync_TwoEvents_RowsDoNotLeakBetweenEvents()
        {
            Guid first = await SeedEventWithDetailsAsync("first");
            Guid second = await SeedEventWithDetailsAsync("second");

            IReadOnlyList<EventDetail> onlyFirst = await _repository.GetDetailsAsync(new[] { first }, CancellationToken.None);
            EventDetail firstDetail = Assert.Single(onlyFirst);
            Assert.Equal("zone-first", Assert.Single(firstDetail.Zones).ZoneId);
            Assert.Equal("profile-first", Assert.Single(firstDetail.People).ProfileId);
            Assert.Equal("alert-first", Assert.Single(firstDetail.SecurityAlerts).AlertText);
            Assert.Equal(2, firstDetail.Occurrences.Count);

            IReadOnlyList<EventDetail> both = await _repository.GetDetailsAsync(new[] { first, second }, CancellationToken.None);
            Assert.Equal(2, both.Count);
            EventDetail secondDetail = both.Single(d => d.EventId == second);
            Assert.Equal("zone-second", Assert.Single(secondDetail.Zones).ZoneId);
            Assert.Equal("profile-second", Assert.Single(secondDetail.People).ProfileId);
            Assert.Equal(2, secondDetail.Occurrences.Count);
        }
    }
}
