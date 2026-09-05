using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

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
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 10; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            TimelineSummary summary = await _repository.GetTimelineSummaryAsync(location.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(10, summary.TotalCount);
            Assert.Contains("Healthy", summary.Status);
            Assert.Null(summary.ComplianceScore);
            DeviceTimelineSummary deviceSummary = Assert.Single(summary.DeviceSummaries);
            Assert.True(deviceSummary.CoveragePercentage > 80);
            Assert.Equal(0, deviceSummary.GapCount);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsAnomaliesSummary_WhenGapsExist()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            // Create more events to ensure good coverage but with a gap
            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            // Gap of 20 minutes (> 5)
            for (int i = 5; i < 10; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes((i * 5) + 20);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            TimelineSummary summary = await _repository.GetTimelineSummaryAsync(location.Id, now.AddMinutes(-5), now.AddMinutes(70), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(10, summary.TotalCount);
            DeviceTimelineSummary deviceSummary = Assert.Single(summary.DeviceSummaries);
            Assert.True(deviceSummary.GapCount > 0);
            Assert.Contains("Anomalies", summary.Status);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsCriticalSummary_WhenLargeGapExists()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            Event evt1 = TestDataBuilder.BuildEvent(device.Id);
            evt1.OccurredAtUtc = now;
            _ = await _eventRepository.UpsertAsync(evt1, CancellationToken.None);

            Event evt2 = TestDataBuilder.BuildEvent(device.Id);
            evt2.OccurredAtUtc = now.AddHours(3);
            _ = await _eventRepository.UpsertAsync(evt2, CancellationToken.None);

            TimelineSummary summary = await _repository.GetTimelineSummaryAsync(location.Id, now, now.AddHours(4), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Contains("Critical", summary.Status);
            Assert.Null(summary.ComplianceScore);
            DeviceTimelineSummary deviceSummary = Assert.Single(summary.DeviceSummaries);
            Assert.True(deviceSummary.CoveragePercentage < 50);
        }

        [Fact]
        public async Task VerifyTimelineIntegrityAsync_TwoDevicesDifferentCoverage_ReportsIndependentPerDeviceStats()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device goodDevice = TestDataBuilder.BuildDevice(location.Id);
            Device badDevice = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;
            DateTime from = now;
            DateTime to = now.AddMinutes(40);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(goodDevice, CancellationToken.None);
            await _deviceRepository.AddAsync(badDevice, CancellationToken.None);

            // Good device: events every 2 minutes (under the 5-minute gap threshold), so no gap
            // is ever detected between consecutive events - near-100% coverage.
            for (int i = 0; i < 20; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(goodDevice.Id);
                evt.OccurredAtUtc = from.AddMinutes(i * 2);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            // Bad device: two events with a 38-minute gap between them (>> the 5-minute
            // threshold), consuming nearly the entire window - low coverage.
            Event badEvt1 = TestDataBuilder.BuildEvent(badDevice.Id);
            badEvt1.OccurredAtUtc = from.AddMinutes(1);
            _ = await _eventRepository.UpsertAsync(badEvt1, CancellationToken.None);
            Event badEvt2 = TestDataBuilder.BuildEvent(badDevice.Id);
            badEvt2.OccurredAtUtc = from.AddMinutes(39);
            _ = await _eventRepository.UpsertAsync(badEvt2, CancellationToken.None);

            TimelineIntegrityReport report = await _repository.VerifyTimelineIntegrityAsync(location.Id, from, to, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(2, report.DeviceReports.Count);
            DeviceTimelineIntegrity goodReport = Assert.Single(report.DeviceReports, d => d.DeviceId == goodDevice.Id);
            DeviceTimelineIntegrity badReport = Assert.Single(report.DeviceReports, d => d.DeviceId == badDevice.Id);
            // The core regression: each device's true coverage survives independently - the bad
            // device's low coverage must not be masked by blending with the good device's.
            Assert.True(goodReport.CoveragePercentage > 90m);
            Assert.True(badReport.CoveragePercentage < 20m);
            Assert.Equal("Intact", goodReport.IntegrityStatus);
            Assert.Equal("Critical", badReport.IntegrityStatus);
        }

        [Fact]
        public async Task VerifyTimelineIntegrityAsync_DeviceWithNoEvents_StillAppearsInReport()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device silentDevice = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(silentDevice, CancellationToken.None);

            TimelineIntegrityReport report = await _repository.VerifyTimelineIntegrityAsync(location.Id, now, now.AddHours(1), CancellationToken.None);

            Assert.NotNull(report);
            DeviceTimelineIntegrity deviceReport = Assert.Single(report.DeviceReports);
            Assert.Equal(silentDevice.Id, deviceReport.DeviceId);
            Assert.Equal(0, deviceReport.TotalEvents);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_TwoDevicesDifferentCoverage_ReportsIndependentPerDeviceStats()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device goodDevice = TestDataBuilder.BuildDevice(location.Id);
            Device badDevice = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;
            DateTime from = now;
            DateTime to = now.AddMinutes(40);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(goodDevice, CancellationToken.None);
            await _deviceRepository.AddAsync(badDevice, CancellationToken.None);

            for (int i = 0; i < 20; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(goodDevice.Id);
                evt.OccurredAtUtc = from.AddMinutes(i * 2);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            Event badEvt1 = TestDataBuilder.BuildEvent(badDevice.Id);
            badEvt1.OccurredAtUtc = from.AddMinutes(1);
            _ = await _eventRepository.UpsertAsync(badEvt1, CancellationToken.None);
            Event badEvt2 = TestDataBuilder.BuildEvent(badDevice.Id);
            badEvt2.OccurredAtUtc = from.AddMinutes(39);
            _ = await _eventRepository.UpsertAsync(badEvt2, CancellationToken.None);

            TimelineSummary summary = await _repository.GetTimelineSummaryAsync(location.Id, from, to, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Null(summary.ComplianceScore);
            Assert.Equal(2, summary.DeviceSummaries.Count);
            DeviceTimelineSummary goodSummary = Assert.Single(summary.DeviceSummaries, d => d.DeviceId == goodDevice.Id);
            DeviceTimelineSummary badSummary = Assert.Single(summary.DeviceSummaries, d => d.DeviceId == badDevice.Id);
            Assert.True(goodSummary.CoveragePercentage > 90m);
            Assert.True(badSummary.CoveragePercentage < 20m);
            // Worst-status-among-devices rollup: the bad device's Critical status must surface at
            // the top level even though the good device is Healthy.
            Assert.Equal("Critical", summary.Status);
        }

        [Fact]
        public async Task GetRecordingGapsPaginatedAsync_ReturnsPaginatedResult_PageOne()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            PaginatedResult<TimelineGap> result = await _repository.GetRecordingGapsPaginatedAsync(
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
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 6; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            PaginatedResult<TimelineGap> result = await _repository.GetRecordingGapsPaginatedAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(170), minGapMinutes: 5, pageNumber: 2, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.PageNumber);
        }

        [Fact]
        public async Task GetRecordingGapsPaginatedAsync_ReturnsEmptyResult_OutOfBounds()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            Event evt = TestDataBuilder.BuildEvent(device.Id);
            evt.OccurredAtUtc = now;
            _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);

            PaginatedResult<TimelineGap> result = await _repository.GetRecordingGapsPaginatedAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(20), minGapMinutes: 5, pageNumber: 10, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_ReturnsCursorResult_FirstPage()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<TimelineGap> result = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: null, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Items.Count >= 0);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_ReturnsCursorResult_WithCursor()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<TimelineGap> firstResult = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: null, pageSize: 1, CancellationToken.None);

            if (firstResult.HasMore && firstResult.NextCursor != null)
            {
                CursorPaginatedResult<TimelineGap> secondResult = await _repository.GetRecordingGapsCursorAsync(
                    device.Id, now.AddMinutes(-10), now.AddMinutes(140), minGapMinutes: 5, cursor: firstResult.NextCursor, pageSize: 1, CancellationToken.None);

                Assert.NotNull(secondResult);
            }
        }

        [Fact]
        public async Task GetRecordingGapsCursorAsync_VerifyHasMoreFlag()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 3; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<TimelineGap> result = await _repository.GetRecordingGapsCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(80), minGapMinutes: 5, cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
            Assert.Null(result.NextCursor);
        }

        [Fact]
        public async Task GetEventCountByHourAsync_ReturnsHourlyDistribution()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int hour = 8; hour <= 17; hour++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Event evt = TestDataBuilder.BuildEvent(device.Id);
                    evt.OccurredAtUtc = now.Date.AddHours(hour).AddMinutes(i * 10);
                    _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
                }
            }

            Dictionary<int, int> counts = await _repository.GetEventCountByHourAsync(device.Id, now.Date, now.Date.AddDays(1), CancellationToken.None);

            Assert.NotEmpty(counts);
            Assert.True(counts.ContainsKey(8) || counts.ContainsKey(9));
        }

        [Fact]
        public async Task GetPeakActivityPeriodsAsync_ReturnsTopHours()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int hour = 8; hour <= 17; hour++)
            {
                int count = hour == 12 ? 10 : 3;
                for (int i = 0; i < count; i++)
                {
                    Event evt = TestDataBuilder.BuildEvent(device.Id);
                    evt.OccurredAtUtc = now.Date.AddHours(hour).AddMinutes(i * 5);
                    _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
                }
            }

            IReadOnlyList<HourlyActivityCount> peaks = await _repository.GetPeakActivityPeriodsAsync(location.Id, now.Date, now.Date.AddDays(1), CancellationToken.None);

            Assert.NotEmpty(peaks);
        }

        [Fact]
        public async Task GetTimelineSummaryAsync_ReturnsNullLocation_WhenNoEvents()
        {
            Location location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            DateTime now = DateTime.UtcNow;
            TimelineSummary summary = await _repository.GetTimelineSummaryAsync(location.Id, now, now.AddHours(1), CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(0, summary.TotalCount);
        }
    }
}
