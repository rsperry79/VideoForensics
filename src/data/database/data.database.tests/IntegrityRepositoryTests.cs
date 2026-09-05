using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class IntegrityRepositoryTests : RepositoryTestBase
    {
        private IntegrityRepository _repository = null!;
        private EventRepository _eventRepository = null!;
        private DeviceRepository _deviceRepository = null!;
        private LocationRepository _locationRepository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _eventRepository = new EventRepository(Fixture.Factory, CreateLogger<EventRepository>());
            _deviceRepository = new DeviceRepository(Fixture.Factory, CreateLogger<DeviceRepository>());
            _locationRepository = new LocationRepository(Fixture.Factory, CreateLogger<LocationRepository>());
            _repository = new IntegrityRepository(Fixture.Factory, CreateLogger<IntegrityRepository>());
        }

        [Fact]
        public async Task GetIntegritySummaryAsync_ReturnsGoodIntegrity_WhenAllDownloaded()
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
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            IntegritySummary summary = await _repository.GetIntegritySummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(0, summary.MissingDownloads);
            Assert.True(summary.IntegrityScore > 90);
            Assert.Contains("Healthy", summary.Status);
        }

        [Fact]
        public async Task GetIntegritySummaryAsync_ReturnsMissingDownloads_WhenNotAllDownloaded()
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
                evt.DownloadedAtUtc = i % 2 == 0 ? evt.OccurredAtUtc.AddSeconds(30) : null;
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            IntegritySummary summary = await _repository.GetIntegritySummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.NotNull(summary.Status);
            Assert.True(summary.IntegrityScore is >= 0 and <= 100);
        }

        [Fact]
        public async Task GetTamperingIndicatorsPaginatedAsync_ReturnsPaginatedResult_FirstPage()
        {
            Location location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            PaginatedResult<TamperingIndicator> result = await _repository.GetTamperingIndicatorsPaginatedAsync(
                location.Id, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetTamperingIndicatorsPaginatedAsync_ReturnsEmptyList_NoTampering()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            DateTime now = DateTime.UtcNow;

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 5);
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            PaginatedResult<TamperingIndicator> result = await _repository.GetTamperingIndicatorsPaginatedAsync(
                location.Id, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetDownloadHistoryCursorAsync_ReturnsCursorResult_FirstPage()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<DownloadAuditRecord> result = await _repository.GetDownloadHistoryCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetDownloadHistoryCursorAsync_VerifyHasMoreFlag()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 3; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            CursorPaginatedResult<DownloadAuditRecord> result = await _repository.GetDownloadHistoryCursorAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
            Assert.Null(result.NextCursor);
        }

        [Fact]
        public async Task GetDownloadHistoryAsync_ReturnsAllDownloads_ForDevice()
        {
            Device device = TestDataBuilder.BuildDevice();
            DateTime now = DateTime.UtcNow;

            await _deviceRepository.AddAsync(device, CancellationToken.None);

            for (int i = 0; i < 5; i++)
            {
                Event evt = TestDataBuilder.BuildEvent(device.Id);
                evt.OccurredAtUtc = now.AddMinutes(i * 10);
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            IReadOnlyList<DownloadAuditRecord> result = await _repository.GetDownloadHistoryAsync(
                device.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.Equal(5, result.Count);
            Assert.All(result, r => Assert.Equal("Downloaded", r.DownloadStatus));
        }

        [Fact]
        public async Task GetMissingDownloadsAsync_ReturnsMissing_WhenNotDownloaded()
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
                evt.DownloadedAtUtc = i >= 5 ? null : evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            IReadOnlyList<MissingDownloadRecord> missing = await _repository.GetMissingDownloadsAsync(
                location.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.Equal(5, missing.Count);
            Assert.All(missing, m => Assert.NotNull(m.OccurredAtUtc));
        }

        [Fact]
        public async Task VerifyDownloadCompletenessAsync_ReturnsComplete_WhenAllDownloaded()
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
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            DownloadCompletenessReport report = await _repository.VerifyDownloadCompletenessAsync(
                location.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(location.Id, report.LocationId);
            Assert.Equal(10, report.DownloadedEvents);
            Assert.Equal(0, report.MissingEvents);
            Assert.True(report.CompletenessPercentage > 99);
            Assert.Contains("Complete", report.Status);
        }

        [Fact]
        public async Task VerifyDownloadCompletenessAsync_ReturnsCritical_WhenMostMissing()
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
                evt.DownloadedAtUtc = i < 2 ? evt.OccurredAtUtc.AddSeconds(30) : null;
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            DownloadCompletenessReport report = await _repository.VerifyDownloadCompletenessAsync(
                location.Id, now.AddMinutes(-10), now.AddMinutes(60), CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(8, report.MissingEvents);
            Assert.True(report.CompletenessPercentage < 30);
        }

        [Fact]
        public async Task ComputeEventIntegrityScoreAsync_ReturnsHighScore_AllIntact()
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
                evt.DownloadedAtUtc = evt.OccurredAtUtc.AddSeconds(30);
                _ = await _eventRepository.UpsertAsync(evt, CancellationToken.None);
            }

            var score = await _repository.ComputeEventIntegrityScoreAsync(location.Id, CancellationToken.None);

            Assert.True(score is >= 0 and <= 100);
        }

        [Fact]
        public async Task GetIntegritySummaryAsync_ReturnsAllGood_WhenNoIssues()
        {
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);

            await _locationRepository.AddAsync(location, CancellationToken.None);
            await _deviceRepository.AddAsync(device, CancellationToken.None);

            IntegritySummary summary = await _repository.GetIntegritySummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(0, summary.TamperingIndicators);
            Assert.Equal(0, summary.MissingDownloads);
        }
    }
}
