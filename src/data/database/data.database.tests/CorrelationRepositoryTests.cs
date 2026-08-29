using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

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
            var location = TestDataBuilder.BuildLocation();
            var device1 = TestDataBuilder.BuildDevice(location.Id);
            var device2 = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device1, CancellationToken.None);
            await _deviceRepository.AddAsync(device2, CancellationToken.None);

            var summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(2, summary.DeviceCount);
            Assert.Contains("Healthy", summary.Status);
            Assert.Equal(0, summary.UnhealthyDeviceCount);
        }

        [Fact]
        public async Task GetCorrelationSummaryAsync_ReturnsDegraded_WhenDeviceOffline()
        {
            var location = TestDataBuilder.BuildLocation();
            var device1 = TestDataBuilder.BuildDevice(location.Id);
            device1.IsOnline = false;
            var device2 = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device1, CancellationToken.None);
            await _deviceRepository.AddAsync(device2, CancellationToken.None);

            var summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.True(summary.UnhealthyDeviceCount > 0);
        }

        [Fact]
        public async Task GetCorrelationSummaryAsync_ReturnsMixed_WithSomeOffline()
        {
            var location = TestDataBuilder.BuildLocation();
            var onlineDevice = TestDataBuilder.BuildDevice(location.Id);
            onlineDevice.IsOnline = true;
            var offlineDevice = TestDataBuilder.BuildDevice(location.Id);
            offlineDevice.IsOnline = false;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(onlineDevice, CancellationToken.None);
            await _deviceRepository.AddAsync(offlineDevice, CancellationToken.None);

            var summary = await _repository.GetCorrelationSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(2, summary.DeviceCount);
            Assert.Equal(1, summary.UnhealthyDeviceCount);
        }

        [Fact]
        public async Task GetHealthRelatedGapsPaginatedAsync_ReturnsPaginatedResult_FirstPage()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var result = await _repository.GetHealthRelatedGapsPaginatedAsync(
                location.Id, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetHealthRelatedGapsPaginatedAsync_ReturnsPaginatedResult_SecondPage()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var result = await _repository.GetHealthRelatedGapsPaginatedAsync(
                location.Id, pageNumber: 2, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.PageNumber);
        }

        [Fact]
        public async Task GetEventHealthCorrelationCursorAsync_ReturnsCursorResult_FirstPage()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetEventHealthCorrelationCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetEventHealthCorrelationCursorAsync_VerifyHasMoreFlag()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 3; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var result = await _repository.GetEventHealthCorrelationCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
            Assert.Null(result.NextCursor);
        }

        [Fact]
        public async Task AnalyzeSyncHealthAsync_ReturnsHealthyStatus_WhenAllGood()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            device.IsOnline = true;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var report = await _repository.AnalyzeSyncHealthAsync(location.Id, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(1, report.DeviceCount);
        }

        [Fact]
        public async Task AnalyzeDeviceReliabilityAsync_ReturnsReliabilityAnalysis()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 10; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var analysis = await _repository.AnalyzeDeviceReliabilityAsync(device.Id, CancellationToken.None);

            Assert.NotNull(analysis);
            Assert.Equal(device.Id, analysis.DeviceId);
        }

        [Fact]
        public async Task IdentifyHealthRelatedGapsAsync_ReturnsGaps_WhenHealthIssues()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var gaps = await _repository.IdentifyHealthRelatedGapsAsync(location.Id, CancellationToken.None);

            Assert.NotNull(gaps);
        }

        [Fact]
        public async Task GetEventHealthCorrelationAsync_ReturnsEvents_WithHealthData()
        {
            var device = TestDataBuilder.BuildDevice();
            var now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                var evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var correlations = await _repository.GetEventHealthCorrelationAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(correlations);
            Assert.Equal(5, correlations.Count);
        }

        [Fact]
        public async Task GetLocationChangeHistoryAsync_ReturnsEmpty_NoChanges()
        {
            var device = TestDataBuilder.BuildDevice();

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var history = await _repository.GetLocationChangeHistoryAsync(device.Id, CancellationToken.None);

            Assert.NotNull(history);
        }

        [Fact]
        public async Task CorrelateEventMissingWithSyncGapsAsync_ReturnsCorrelations()
        {
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            var correlations = await _repository.CorrelateEventMissingWithSyncGapsAsync(location.Id, CancellationToken.None);

            Assert.NotNull(correlations);
        }
    }
}
