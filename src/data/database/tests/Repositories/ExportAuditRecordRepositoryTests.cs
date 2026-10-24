using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for ExportAuditRecordRepository.
    /// Verifies export operation audit trail tracking and compliance reporting.
    /// </summary>
    public class ExportAuditRecordRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ExportAuditRecordRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ExportAuditRecordRepository(_fixture.Factory, loggerFactory.CreateLogger<ExportAuditRecordRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ExportAuditRecordRepository_RecordExportAsync_CreatesExportRecord()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            string exportedBy = "analyst@example.com";
            int eventsExported = 150;
            string exportFormat = "AES256Zip";
            string purpose = "Case evidence packaging for prosecution";

            // Act
            ExportAuditRecordEntity record = await _repository.RecordExportAsync(
                locationId, exportedBy, eventsExported, exportFormat, purpose, CancellationToken.None);

            // Assert
            Assert.NotNull(record);
            Assert.NotEqual(Guid.Empty, record.Id);
            Assert.Equal(locationId, record.LocationId);
            Assert.Equal(exportedBy, record.ExportedBy);
            Assert.Equal(eventsExported, record.EventsExported);
            Assert.Equal(exportFormat, record.ExportFormat);
            Assert.Equal(purpose, record.Purpose);
        }

        [Fact]
        public async Task ExportAuditRecordRepository_GetAsync_FindsExportRecord()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            ExportAuditRecordEntity record = await _repository.RecordExportAsync(
                locationId, "analyst@example.com", 100, "PDF", "Report generation", CancellationToken.None);

            // Act
            ExportAuditRecordEntity? retrieved = await _repository.GetAsync(record.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(record.Id, retrieved.Id);
            Assert.Equal(locationId, retrieved.LocationId);
        }

        [Fact]
        public async Task ExportAuditRecordRepository_GetForLocationAsync_ReturnsLocationExports()
        {
            // Arrange
            var locationId = Guid.NewGuid();
            _ = await _repository.RecordExportAsync(locationId, "user1@example.com", 50, "AES256Zip", "Export1", CancellationToken.None);
            _ = await _repository.RecordExportAsync(locationId, "user2@example.com", 75, "PDF", "Export2", CancellationToken.None);

            // Act
            IReadOnlyList<ExportAuditRecordEntity> exports = await _repository.GetForLocationAsync(locationId, CancellationToken.None);

            // Assert
            Assert.Equal(2, exports.Count);
            Assert.All(exports, e => Assert.Equal(locationId, e.LocationId));
        }

        [Fact]
        public async Task ExportAuditRecordRepository_GetByUserAsync_ReturnsUserExports()
        {
            // Arrange
            string userId = "analyst@example.com";
            var location1 = Guid.NewGuid();
            var location2 = Guid.NewGuid();
            _ = await _repository.RecordExportAsync(location1, userId, 100, "AES256Zip", "Export1", CancellationToken.None);
            _ = await _repository.RecordExportAsync(location2, userId, 200, "PDF", "Export2", CancellationToken.None);

            // Act
            IReadOnlyList<ExportAuditRecordEntity> exports = await _repository.GetByUserAsync(userId, CancellationToken.None);

            // Assert
            Assert.Equal(2, exports.Count);
            Assert.All(exports, e => Assert.Equal(userId, e.ExportedBy));
        }

        [Fact]
        public async Task ExportAuditRecordRepository_GetStatisticsAsync_CalculatesCorrectly()
        {
            // Arrange
            var location1 = Guid.NewGuid();
            var location2 = Guid.NewGuid();
            _ = await _repository.RecordExportAsync(location1, "user1@example.com", 100, "AES256Zip", "Export1", CancellationToken.None);
            _ = await _repository.RecordExportAsync(location2, "user2@example.com", 50, "PDF", "Export2", CancellationToken.None);
            _ = await _repository.RecordExportAsync(location1, "user1@example.com", 75, "AES256Zip", "Export3", CancellationToken.None);

            // Act
            ExportStatistics stats = await _repository.GetStatisticsAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, stats.TotalExports);
            Assert.Equal(225, stats.TotalEventsExported); // 100 + 50 + 75
            Assert.Equal(2, stats.UniqueExporters); // user1 and user2
            Assert.Equal(2, stats.ExportsByFormat.Count);
            Assert.Equal(2, stats.ExportsByFormat["AES256Zip"]);
            Assert.Equal(1, stats.ExportsByFormat["PDF"]);
        }
    }
}
