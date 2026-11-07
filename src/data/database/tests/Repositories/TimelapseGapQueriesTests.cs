using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>Queries the event-gap time-lapse planner needs: a device's event times (without its own time lapses) and its latest time-lapse end.</summary>
    public class TimelapseGapQueries_Tests : IAsyncLifetime
    {
        private static readonly DateTime Start = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        private const string LoopMetadata = "{\"type\":\"snapshot-rollup\"}";

        private SqliteInMemoryFixture _fixture = null!;
        private EventRepository _events = null!;
        private MediaItemRepository _media = null!;
        private TimelapseGapQueries _queries = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _events = new EventRepository(_fixture.Factory, NullLogger<EventRepository>.Instance);
            _media = new MediaItemRepository(_fixture.Factory, NullLogger<MediaItemRepository>.Instance);
            _queries = new TimelapseGapQueries(_fixture.Factory, NullLogger<TimelapseGapQueries>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static Event Ev(Guid deviceId, int minute, string eventType = "motion")
        {
            return new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                ProviderEventId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                OccurredAtUtc = Start.AddMinutes(minute),
                DiscoveredAtUtc = Start,
            };
        }

        private static MediaItem Loop(Guid deviceId, int minute, double seconds)
        {
            return new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = $"loop-{minute}",
                FilePath = $"loop-{minute}",
                MediaFormat = "mp4",
                Sha256Hash = Guid.NewGuid().ToString("N"),
                RecordedAtUtc = Start.AddMinutes(minute),
                DurationSeconds = seconds,
                DownloadedAtUtc = Start,
                MetadataJson = LoopMetadata,
            };
        }

        [Fact]
        public async Task EventRepository_ListEventTimesForGaps_ExcludesOwnTimelapsesAndOtherDevices_AscendingOrder()
        {
            var device = Guid.NewGuid();
            await _events.CreateAsync(Ev(device, 30), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 10), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 20, "timelapse"), CancellationToken.None);
            await _events.CreateAsync(Ev(Guid.NewGuid(), 15), CancellationToken.None);

            IReadOnlyList<DateTime> times = await _queries.ListEventTimesForGapsAsync(device, Start, Start.AddHours(1), CancellationToken.None);

            Assert.Equal(new[] { Start.AddMinutes(10), Start.AddMinutes(30) }, times);
        }

        [Fact]
        public async Task MediaItemRepository_LatestTimelapseEnd_IsNewestLoopStartPlusDuration()
        {
            var device = Guid.NewGuid();
            await _media.AddAsync(Loop(device, 0, 43 * 60), CancellationToken.None);
            await _media.AddAsync(Loop(device, 60, 1800), CancellationToken.None);

            DateTime? end = await _queries.GetLatestTimelapseEndUtcAsync(device, CancellationToken.None);

            Assert.Equal(Start.AddMinutes(60).AddSeconds(1800), end);
        }

        [Fact]
        public async Task SnapshotCaptures_EarliestCapture_IsOldestAttemptForTheDevice()
        {
            var locations = new LocationRepository(_fixture.Factory, NullLogger<LocationRepository>.Instance);
            var devices = new DeviceRepository(_fixture.Factory, NullLogger<DeviceRepository>.Instance);
            Location location = TestDataBuilder.BuildLocation(name: "Gap Site");
            await locations.AddAsync(location, CancellationToken.None);
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Gap Camera");
            await devices.AddAsync(device, CancellationToken.None);
            Device other = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Other Camera");
            await devices.AddAsync(other, CancellationToken.None);

            var captures = new SnapshotCaptureRepository(_fixture.Factory, NullLogger<SnapshotCaptureRepository>.Instance);
            await captures.AddAsync(SnapshotCapture.Create(device.Id, new DateTimeOffset(Start.AddMinutes(20), TimeSpan.Zero), "fresh", 404, null, null), CancellationToken.None);
            await captures.AddAsync(SnapshotCapture.Create(device.Id, new DateTimeOffset(Start.AddMinutes(5), TimeSpan.Zero), "fresh", 200, "p.jpg", null), CancellationToken.None);
            await captures.AddAsync(SnapshotCapture.Create(other.Id, new DateTimeOffset(Start.AddMinutes(1), TimeSpan.Zero), "fresh", 200, "q.jpg", null), CancellationToken.None);

            DateTimeOffset? earliest = await _queries.GetEarliestCaptureAtUtcAsync(device.Id, CancellationToken.None);

            Assert.Equal(new DateTimeOffset(Start.AddMinutes(5), TimeSpan.Zero), earliest);
        }

        [Fact]
        public async Task EventRepository_LatestEventTime_IsNewestAtOrBeforeTheHorizonExcludingTimelapses()
        {
            var device = Guid.NewGuid();
            await _events.CreateAsync(Ev(device, 10), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 30, "timelapse"), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 50), CancellationToken.None);

            DateTime? latest = await _queries.GetLatestEventTimeUtcAsync(device, Start.AddMinutes(40), CancellationToken.None);

            Assert.Equal(Start.AddMinutes(10), latest);
        }

        [Fact]
        public async Task EventRepository_LatestEventTimeAny_ReturnsFutureDatedEvent()
        {
            var device = Guid.NewGuid();
            await _events.CreateAsync(Ev(device, 10), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 300), CancellationToken.None);
            await _events.CreateAsync(Ev(device, 320, "timelapse"), CancellationToken.None);

            DateTime? latest = await _queries.GetLatestEventTimeAnyUtcAsync(device, CancellationToken.None);

            Assert.Equal(Start.AddHours(5), latest);
        }

        [Fact]
        public async Task MediaItemRepository_LatestTimelapseEnd_NullWhenDeviceHasNoLoops()
        {
            DateTime? end = await _queries.GetLatestTimelapseEndUtcAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(end);
        }
    }
}
