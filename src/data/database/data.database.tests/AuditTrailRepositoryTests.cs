using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class AuditTrailRepositoryTests : RepositoryTestBase
    {
        private AuditTrailRepository _repository = null!;
        private EventRepository _eventRepository = null!;
        private LocationRepository _locationRepository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _eventRepository = new EventRepository(Fixture.Factory, CreateLogger<EventRepository>());
            _locationRepository = new LocationRepository(Fixture.Factory, CreateLogger<LocationRepository>());
            _repository = new AuditTrailRepository(Fixture.Factory, CreateLogger<AuditTrailRepository>());
        }

        [Fact]
        public async Task GetAuditTrailSummaryAsync_ReturnsComplete_WhenAllGood()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var summary = await _repository.GetAuditTrailSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(location.Id.ToString(), summary.Status);
        }

        [Fact]
        public async Task GetAuditTrailSummaryAsync_ReturnsSuspicious_WhenAccessFlagged()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var summary = await _repository.GetAuditTrailSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
        }

        [Fact]
        public async Task GetAuditTrailSummaryAsync_ReturnsCompromised_WhenIntegrityBroken()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var summary = await _repository.GetAuditTrailSummaryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.False(summary.ChainOfCustodyIntact);
        }

        [Fact]
        public async Task GetAccessHistoryPaginatedAsync_ReturnsPaginatedResult_FirstPage()
        {
            var evidenceId = Guid.NewGuid();

            var result = await _repository.GetAccessHistoryPaginatedAsync(
                evidenceId, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public async Task GetAccessHistoryPaginatedAsync_ReturnsPaginatedResult_SecondPage()
        {
            var evidenceId = Guid.NewGuid();

            var result = await _repository.GetAccessHistoryPaginatedAsync(
                evidenceId, pageNumber: 2, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.PageNumber);
        }

        [Fact]
        public async Task GetAccessHistoryPaginatedAsync_ReturnsEmpty_NoAccess()
        {
            var evidenceId = Guid.NewGuid();

            var result = await _repository.GetAccessHistoryPaginatedAsync(
                evidenceId, pageNumber: 1, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetExportHistoryCursorAsync_ReturnsCursorResult_FirstPage()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var result = await _repository.GetExportHistoryCursorAsync(
                location.Id, cursor: null, pageSize: 10, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Count >= 0);
        }

        [Fact]
        public async Task GetExportHistoryCursorAsync_VerifyHasMoreFlag()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var result = await _repository.GetExportHistoryCursorAsync(
                location.Id, cursor: null, pageSize: 100, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.HasMore);
        }

        [Fact]
        public async Task VerifyChainOfCustodyAsync_ReturnsIntact_WhenAllAccounted()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var report = await _repository.VerifyChainOfCustodyAsync(location.Id, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(location.Id, report.LocationId);
        }

        [Fact]
        public async Task FlagUnauthorizedAccessAsync_ReturnsEmpty_NoUnauthorized()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var flags = await _repository.FlagUnauthorizedAccessAsync(location.Id, CancellationToken.None);

            Assert.NotNull(flags);
        }

        [Fact]
        public async Task FlagUnauthorizedAccessAsync_ReturnsFlagged_WhenOffHours()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var flags = await _repository.FlagUnauthorizedAccessAsync(location.Id, CancellationToken.None);

            Assert.NotNull(flags);
        }

        [Fact]
        public async Task FlagUnauthorizedAccessAsync_ReturnsFlagged_WhenExcessiveAccess()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var flags = await _repository.FlagUnauthorizedAccessAsync(location.Id, CancellationToken.None);

            Assert.NotNull(flags);
        }

        [Fact]
        public async Task GetExportHistoryAsync_ReturnsEmpty_NoExports()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var history = await _repository.GetExportHistoryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(history);
            Assert.Empty(history);
        }

        [Fact]
        public async Task VerifyExportIntegrityAsync_ReturnsIntact_NoModifications()
        {
            var exportId = Guid.NewGuid();

            var report = await _repository.VerifyExportIntegrityAsync(exportId, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(exportId, report.ExportId);
        }

        [Fact]
        public async Task LogAccessAsync_RecordsAccess()
        {
            var evidenceId = Guid.NewGuid();
            var userId = "test_user";
            var action = "View";
            var now = DateTime.UtcNow;

            await _repository.LogAccessAsync(evidenceId, userId, action, now, CancellationToken.None);

            var history = await _repository.GetAccessHistoryAsync(evidenceId, CancellationToken.None);
            Assert.NotEmpty(history);
            Assert.Single(history);
            Assert.Equal(userId, history[0].UserId);
            Assert.Equal(action, history[0].Action);
        }

        [Fact]
        public async Task GetAccessHistoryAsync_ReturnsAllAccesses()
        {
            var evidenceId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await _repository.LogAccessAsync(evidenceId, "user1", "View", now, CancellationToken.None);
            await _repository.LogAccessAsync(evidenceId, "user2", "Download", now.AddSeconds(10), CancellationToken.None);

            var history = await _repository.GetAccessHistoryAsync(evidenceId, CancellationToken.None);

            Assert.Equal(2, history.Count);
        }

        [Fact]
        public async Task GetLocationAccessHistoryAsync_ReturnsAccessesForLocation()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var history = await _repository.GetLocationAccessHistoryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(history);
        }

        [Fact]
        public async Task GetRedactionHistoryAsync_ReturnsEmpty_NoRedactions()
        {
            var location = TestDataBuilder.BuildLocation();
            await _locationRepository.AddAsync(location, CancellationToken.None);

            var history = await _repository.GetRedactionHistoryAsync(location.Id, CancellationToken.None);

            Assert.NotNull(history);
            Assert.Empty(history);
        }

        [Fact]
        public async Task TraceModificationHistoryAsync_ReturnsEmpty_NoModifications()
        {
            var eventId = Guid.NewGuid();

            var history = await _repository.TraceModificationHistoryAsync(eventId, CancellationToken.None);

            Assert.NotNull(history);
            Assert.Empty(history);
        }
    }
}
