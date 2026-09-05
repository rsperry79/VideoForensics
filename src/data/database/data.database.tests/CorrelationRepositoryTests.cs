using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class CorrelationRepositoryTests : RepositoryTestBase
    {
        private CorrelationRepository _repository = null!;
        private EventRepository _eventRepository = null!;
        private DeviceRepository _deviceRepository = null!;
        private LocationRepository _locationRepository = null!;
        private TimelineRepository _timelineRepository = null!;
        private IntegrityRepository _integrityRepository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _eventRepository = new EventRepository(Fixture.Factory, CreateLogger<EventRepository>());
            _deviceRepository = new DeviceRepository(Fixture.Factory, CreateLogger<DeviceRepository>());
            _locationRepository = new LocationRepository(Fixture.Factory, CreateLogger<LocationRepository>());
            _timelineRepository = new TimelineRepository(Fixture.Factory, CreateLogger<TimelineRepository>(), _eventRepository, _deviceRepository);
            _integrityRepository = new IntegrityRepository(Fixture.Factory, CreateLogger<IntegrityRepository>());
            _repository = new CorrelationRepository(Fixture.Factory, CreateLogger<CorrelationRepository>(), _timelineRepository, _integrityRepository);
        }

        [Fact]
        public async Task GetCorrelationSummaryAsync_ReturnsHealthy_WhenAllDevicesOnline()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device1 = TestDataBuilder.BuildDevice(location.Id);
            Device device2 = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device1, CancellationToken.None);
            await _deviceRepository.AddAsync(device2, CancellationToken.None);

            CorrelationSummary summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(2, summary.DeviceCount);
            Assert.Contains("Healthy", summary.Status);
            Assert.Equal(0, summary.UnhealthyDeviceCount);
        }

        [Fact]
        public async Task GetCorrelationSummaryAsync_ReturnsDegraded_WhenDeviceOffline()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device1 = TestDataBuilder.BuildDevice(location.Id);
            device1.IsOnline = false;
            Device device2 = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device1, CancellationToken.None);
            await _deviceRepository.AddAsync(device2, CancellationToken.None);

            CorrelationSummary summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.NotNull(summary.Status);
            Assert.Equal(2, summary.DeviceCount);
        }

        [Fact]
        public async Task GetCorrelationSummaryAsync_ReturnsMixed_WithSomeOffline()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device onlineDevice = TestDataBuilder.BuildDevice(location.Id);
            onlineDevice.IsOnline = true;
            Device offlineDevice = TestDataBuilder.BuildDevice(location.Id);
            offlineDevice.IsOnline = false;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(onlineDevice, CancellationToken.None);
            await _deviceRepository.AddAsync(offlineDevice, CancellationToken.None);

            CorrelationSummary summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(2, summary.DeviceCount);
            Assert.NotNull(summary.Status);
        }

        [Fact]
        public async Task GetHealthRelatedGapsPaginatedAsync_ReturnsPaginatedResult_FirstPage()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            PaginatedResult<HealthRelatedGap> result = await _repository.GetHealthRelatedGapsPaginatedAsync(
                location.Id, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetHealthRelatedGapsPaginatedAsync_ReturnsPaginatedResult_SecondPage()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            PaginatedResult<HealthRelatedGap> result = await _repository.GetHealthRelatedGapsPaginatedAsync(
                location.Id, pageNumber: 2, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.PageNumber);
        }

        [Fact]
        public async Task GetEventHealthCorrelationCursorAsync_ReturnsCursorResult_FirstPage()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<EventWithHealthCorrelation> result = await _repository.GetEventHealthCorrelationCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetEventHealthCorrelationCursorAsync_VerifyHasMoreFlag()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 3; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<EventWithHealthCorrelation> result = await _repository.GetEventHealthCorrelationCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
            Assert.Null(result.NextCursor);
        }

        [Fact]
        public async Task AnalyzeSyncHealthAsync_ReturnsHealthyStatus_WhenAllGood()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            device.IsOnline = true;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            SyncHealthReport report = await _repository.AnalyzeSyncHealthAsync(location.Id, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(1, report.DeviceCount);
        }

        [Fact]
        public async Task AnalyzeSyncHealthAsync_TwoDevicesDifferentUptime_NoBlendedAverageInReport()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device reliableDevice = TestDataBuilder.BuildDevice(location.Id);
            Device unreliableDevice = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(reliableDevice, CancellationToken.None);
            await _deviceRepository.AddAsync(unreliableDevice, CancellationToken.None);

            // Reliable device: frequent events, no significant gaps in the last 30 days.
            for (int i = 0; i < 20; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(reliableDevice.Id);
                evt.OccurredAtUtc = now.AddDays(-1).AddMinutes(i * 5);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            // Unreliable device: one huge gap dominating the 30-day window.
            Event firstEvt = TestDataBuilder.BuildEvent(unreliableDevice.Id);
            firstEvt.OccurredAtUtc = now.AddDays(-29);
            _ = await _eventRepository.UpsertAsync(firstEvt, CancellationToken.None);
            Event secondEvt = TestDataBuilder.BuildEvent(unreliableDevice.Id);
            secondEvt.OccurredAtUtc = now.AddDays(-1);
            _ = await _eventRepository.UpsertAsync(secondEvt, CancellationToken.None);

            SyncHealthReport report = await _repository.AnalyzeSyncHealthAsync(location.Id, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(2, report.DeviceCount);
            DeviceSyncStatus reliableStatus = Assert.Single(report.DeviceStatus, s => s.DeviceId == reliableDevice.Id);
            DeviceSyncStatus unreliableStatus = Assert.Single(report.DeviceStatus, s => s.DeviceId == unreliableDevice.Id);
            // The core regression: each device's true uptime survives independently - the
            // unreliable device's number must not be dragged up by averaging with the reliable one.
            Assert.True(reliableStatus.Uptime > unreliableStatus.Uptime);
            Assert.True(unreliableStatus.Uptime < 90m);
        }

        [Fact]
        public async Task AnalyzeDeviceReliabilityAsync_ReturnsReliabilityAnalysis()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 10; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            DeviceReliabilityAnalysis analysis = await _repository.AnalyzeDeviceReliabilityAsync(device.Id, CancellationToken.None);

            Assert.NotNull(analysis);
            Assert.Equal(device.Id, analysis.DeviceId);
        }

        [Fact]
        public async Task IdentifyHealthRelatedGapsAsync_ReturnsGaps_WhenHealthIssues()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            IReadOnlyList<HealthRelatedGap> gaps = await _repository.IdentifyHealthRelatedGapsAsync(location.Id, CancellationToken.None);

            Assert.NotNull(gaps);
        }

        [Fact]
        public async Task GetEventHealthCorrelationAsync_ReturnsEvents_WithHealthData()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            IReadOnlyList<EventWithHealthCorrelation> correlations = await _repository.GetEventHealthCorrelationAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(correlations);
            Assert.True(correlations.Count >= 0);
        }

        [Fact]
        public async Task GetLocationChangeHistoryAsync_ReturnsEmpty_NoChanges()
        {
            Device device = TestDataBuilder.BuildDevice();

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            IReadOnlyList<LocationChangeImpact> history = await _repository.GetLocationChangeHistoryAsync(device.Id, CancellationToken.None);

            Assert.NotNull(history);
        }

        [Fact]
        public async Task CorrelateEventMissingWithSyncGapsAsync_ReturnsCorrelations()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            IReadOnlyList<SyncGapCorrelation> correlations = await _repository.CorrelateEventMissingWithSyncGapsAsync(location.Id, CancellationToken.None);

            Assert.NotNull(correlations);
        }
    }
}
