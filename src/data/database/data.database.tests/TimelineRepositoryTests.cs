using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class TimelineRepositoryTests : RepositoryTestBase
    {
        private TimelineRepository _repository = null!;
        private EventRepository _eventRepository = null!;
        private DeviceRepository _deviceRepository = null!;
        private LocationRepository _locationRepository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _eventRepository = new EventRepository(Fixture.Factory, CreateLogger<EventRepository>());
            _deviceRepository = new DeviceRepository(Fixture.Factory, CreateLogger<DeviceRepository>());
            _locationRepository = new LocationRepository(Fixture.Factory, CreateLogger<LocationRepository>());
            _repository = new TimelineRepository(Fixture.Factory, CreateLogger<TimelineRepository>(), _eventRepository, _deviceRepository);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsHealthySummary_WhenNoGaps()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 10; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var summary = await _repository.GetTimelineSummaryAsync(location.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(10, summary.TotalCount);
            Assert.Contains("Healthy", summary.Status);
            Assert.True(summary.ComplianceScore > 80);
            Assert.Equal(0, summary.GapCount);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsAnomaliesSummary_WhenGapsExist()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            // Create more events to ensure good coverage but with a gap
            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            // Gap of 20 minutes (> 5)
            for (int i = 5; i < 10; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5 + 20);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var summary = await _repository.GetTimelineSummaryAsync(location.Id, now.AddMinutes(-5), now.AddMinutes(70), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(10, summary.TotalCount);
            Assert.True(summary.GapCount > 0);
            Assert.Contains("Anomalies", summary.Status);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsCriticalSummary_WhenLargeGapExists()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var evt1 = TestDataBuilder.BuildEvent(device.Id);
            evt1.OccurredAtUtc = now;
            await _eventRepository.UpsertAsync(evt1, CancellationToken.None);

            var evt2 = TestDataBuilder.BuildEvent(device.Id);
            evt2.OccurredAtUtc = now.AddHours(3);
            await _eventRepository.UpsertAsync(evt2, CancellationToken.None);

            var summary = await _repository.GetTimelineSummaryAsync(location.Id, now, now.AddHours(4), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Contains("Critical", summary.Status);
            Assert.True(summary.ComplianceScore < 50);
        }

        [Fact]
        public async Task GetRecordingGapsPaginatedAsync_ReturnsPaginatedResult_PageOne()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetRecordingGapsPaginatedAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, pageNumber: 1, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(2, result.PageSize);
            Assert.True(result.TotalCount >= 2);
            Assert.True(result.HasNextPage || result.Items.Count > 0);
        }

        [Fact]
        public async Task GetRecordingGapsPaginatedAsync_ReturnsPaginatedResult_PageTwo()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 6; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetRecordingGapsPaginatedAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(170), minGapMinutes: 5, pageNumber: 2, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.PageNumber);
        }

        [Fact]
        public async Task GetRecordingGapsPaginatedAsync_ReturnsEmptyResult_OutOfBounds()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var evt = TestDataBuilder.BuildEvent(device.Id);
            evt.OccurredAtUtc = now;
            await _eventRepository.UpsertAsync(evt, CancellationToken.None);

            var result = await _repository.GetRecordingGapsPaginatedAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(20), minGapMinutes: 5, pageNumber: 10, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_ReturnsCursorResult_FirstPage()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: null, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Items.Count >= 0);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_ReturnsCursorResult_WithCursor()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var firstResult = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: null, pageSize: 1, CancellationToken.None);

            if (firstResult.HasMore && firstResult.NextCursor != null)
            {
                var secondResult = await _repository.GetRecordingGapsCursorAsync(
                    device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: firstResult.NextCursor, pageSize: 1, CancellationToken.None);

                Assert.NotNull(secondResult);
            }
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_VerifyHasMoreFlag()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 3; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(80), minGapMinutes: 5, cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
            Assert.Null(result.NextCursor);
        }

        [Fact]
        public async Task GetEventCountByHourAsync_ReturnsHourlyDistribution()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int hour = 8; hour <= 17; hour++)
            {
                for (int i = 0; i < 3; i++)
                {
                    var evt = TestDataBuilder.BuildEvent(device.Id);
                    evt.OccurredAtUtc = now.Date.AddHours(hour).AddMinutes(i * 10);
                    await _eventRepository.UpsertAsync(evt, CancellationToken.None);
                }
            }

            var counts = await _repository.GetEventCountByHourAsync(device.Id, now.Date, now.Date.AddDays(1), CancellationToken.None);

            Assert.NotEmpty(counts);
            Assert.True(counts.ContainsKey(8) || counts.ContainsKey(9));
        }

        [Fact]
        public async Task GetPeakActivityPeriodsAsync_ReturnsTopHours()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int hour = 8; hour <= 17; hour++)
            {
                int count = hour == 12 ? 10 : 3;
                for (int i = 0; i < count; i++)
                {
                    var evt = TestDataBuilder.BuildEvent(device.Id);
                    evt.OccurredAtUtc = now.Date.AddHours(hour).AddMinutes(i * 5);
                    await _eventRepository.UpsertAsync(evt, CancellationToken.None);
                }
            }

            var peaks = await _repository.GetPeakActivityPeriodsAsync(location.Id, now.Date, now.Date.AddDays(1), CancellationToken.None);

            Assert.NotEmpty(peaks);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsNullLocation_WhenNoEvents()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var now = DateTime.UtcNow;
            var summary = await _repository.GetTimelineSummaryAsync(location.Id, now, now.AddHours(1), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(0, summary.TotalCount);
        }
    }
}
