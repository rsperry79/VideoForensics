using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for AccessAuditLogRepository.
    /// Verifies evidence access audit trail tracking and compliance logging.
    /// </summary>
    public class AccessAuditLogRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private AccessAuditLogRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new AccessAuditLogRepository(_fixture.Factory, loggerFactory.CreateLogger<AccessAuditLogRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task AccessAuditLogRepository_RecordAccessAsync_CreatesAuditEntry()
        {
            // Arrange
            var evidenceId = Guid.NewGuid();
            string userId = "user@example.com";
            string action = "Download";
            string ipAddress = "192.168.1.1";
            string purpose = "Evidence analysis for case #12345";

            // Act
            AccessAuditLogEntity entry = await _repository.RecordAccessAsync(evidenceId, userId, action, ipAddress, purpose, CancellationToken.None);

            // Assert
            Assert.NotNull(entry);
            Assert.NotEqual(Guid.Empty, entry.Id);
            Assert.Equal(evidenceId, entry.EvidenceId);
            Assert.Equal(userId, entry.UserId);
            Assert.Equal(action, entry.Action);
            Assert.Equal(ipAddress, entry.IpAddress);
            Assert.Equal(purpose, entry.Purpose);
        }

        [Fact]
        public async Task AccessAuditLogRepository_GetAsync_FindsRecordedEntry()
        {
            // Arrange
            var evidenceId = Guid.NewGuid();
            AccessAuditLogEntity entry = await _repository.RecordAccessAsync(
                evidenceId, "user@example.com", "View", "192.168.1.1", "Investigation", CancellationToken.None);

            // Act
            AccessAuditLogEntity? retrieved = await _repository.GetAsync(entry.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(entry.Id, retrieved.Id);
            Assert.Equal(evidenceId, retrieved.EvidenceId);
        }

        [Fact]
        public async Task AccessAuditLogRepository_GetForEvidenceAsync_ReturnsAllAccessToEvidence()
        {
            // Arrange
            var evidenceId = Guid.NewGuid();
            _ = await _repository.RecordAccessAsync(evidenceId, "user1@example.com", "View", "192.168.1.1", "Review", CancellationToken.None);
            _ = await _repository.RecordAccessAsync(evidenceId, "user2@example.com", "Download", "192.168.1.2", "Export", CancellationToken.None);

            // Act
            IReadOnlyList<AccessAuditLogEntity> entries = await _repository.GetForEvidenceAsync(evidenceId, CancellationToken.None);

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.All(entries, e => Assert.Equal(evidenceId, e.EvidenceId));
        }

        [Fact]
        public async Task AccessAuditLogRepository_GetByUserAsync_ReturnsAllUserAccess()
        {
            // Arrange
            string userId = "auditor@example.com";
            var evidence1 = Guid.NewGuid();
            var evidence2 = Guid.NewGuid();
            _ = await _repository.RecordAccessAsync(evidence1, userId, "View", "192.168.1.1", "Check1", CancellationToken.None);
            _ = await _repository.RecordAccessAsync(evidence2, userId, "Download", "192.168.1.1", "Check2", CancellationToken.None);

            // Act
            IReadOnlyList<AccessAuditLogEntity> entries = await _repository.GetByUserAsync(userId, CancellationToken.None);

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.All(entries, e => Assert.Equal(userId, e.UserId));
        }

        [Fact]
        public async Task AccessAuditLogRepository_GetByDateRangeAsync_FiltersTimeRange()
        {
            // Arrange
            var evidenceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            _ = await _repository.RecordAccessAsync(evidenceId, "user@example.com", "View", "192.168.1.1", "Test", CancellationToken.None);

            // Act
            IReadOnlyList<AccessAuditLogEntity> entriesInRange = await _repository.GetByDateRangeAsync(now.AddMinutes(-1), now.AddMinutes(1), CancellationToken.None);
            IReadOnlyList<AccessAuditLogEntity> entriesOutOfRange = await _repository.GetByDateRangeAsync(now.AddHours(1), now.AddHours(2), CancellationToken.None);

            // Assert
            Assert.NotEmpty(entriesInRange);
            Assert.Empty(entriesOutOfRange);
        }
    }
}
