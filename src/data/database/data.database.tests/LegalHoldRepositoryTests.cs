using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for LegalHoldRepository.
    /// Verifies legal hold placement, release, and active hold queries with chain-of-custody logging.
    /// </summary>
    public class LegalHoldRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private LegalHoldRepository _repository = null!;
        private ActionLogRepository _actionLogRepository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });

            // Create the ActionLogRepository first since LegalHoldRepository depends on it
            _actionLogRepository = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _repository = new LegalHoldRepository(_fixture.Factory, _actionLogRepository, _loggerFactory.CreateLogger<LegalHoldRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task LegalHoldRepository_PlaceAsync_CreatesLegalHold()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var reason = "Active litigation - preserve all evidence";
            var createdBy = "analyst@example.com";

            // Act
            var hold = await _repository.PlaceAsync(mediaItemId, reason, createdBy, CancellationToken.None);

            // Assert
            Assert.NotNull(hold);
            Assert.NotEqual(Guid.Empty, hold.Id);
            Assert.Equal(mediaItemId, hold.MediaItemId);
            Assert.Equal(reason, hold.Reason);
            Assert.Equal(createdBy, hold.CreatedBy);
            Assert.NotEqual(DateTime.MinValue, hold.CreatedAtUtc);
            Assert.Null(hold.ReleasedAtUtc);
            Assert.Null(hold.ReleasedBy);
            Assert.Null(hold.ReleaseReason);
        }

        [Fact]
        public async Task LegalHoldRepository_PlaceAsync_CreatesChainOfCustodyEntry()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var reason = "Subpoena - case #2026-12345";
            var createdBy = "investigator@example.com";

            // Act
            var hold = await _repository.PlaceAsync(mediaItemId, reason, createdBy, CancellationToken.None);

            // Assert - Verify action log entry was created
            var history = await _actionLogRepository.GetHistoryForEntityAsync("MediaItem", mediaItemId, CancellationToken.None);
            Assert.NotEmpty(history);

            var placeLogEntry = history.FirstOrDefault(e => e.Action == "PlaceLegalHold");
            Assert.NotNull(placeLogEntry);
            Assert.Equal(createdBy, placeLogEntry.Actor);
            Assert.Equal(mediaItemId, placeLogEntry.EntityId);
            Assert.Contains(reason, placeLogEntry.DetailsJson ?? "");
        }

        [Fact]
        public async Task LegalHoldRepository_PlaceAsync_MultipleLegalHoldsOnSameMediaItem()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var reason1 = "Litigation hold";
            var reason2 = "Investigation hold";

            // Act
            var hold1 = await _repository.PlaceAsync(mediaItemId, reason1, "user1@example.com", CancellationToken.None);
            var hold2 = await _repository.PlaceAsync(mediaItemId, reason2, "user2@example.com", CancellationToken.None);

            // Assert
            Assert.NotEqual(hold1.Id, hold2.Id);
            Assert.Equal(mediaItemId, hold1.MediaItemId);
            Assert.Equal(mediaItemId, hold2.MediaItemId);
            Assert.Equal(reason1, hold1.Reason);
            Assert.Equal(reason2, hold2.Reason);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_ReleasesActiveHold()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Temp hold", "user@example.com", CancellationToken.None);
            var releasedBy = "supervisor@example.com";
            var releaseReason = "Case resolved";

            // Act
            await _repository.ReleaseAsync(hold.Id, releasedBy, releaseReason, CancellationToken.None);

            // Assert - Verify the hold is released by fetching active holds
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            Assert.Empty(activeHolds);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_UpdatesHoldWithReleaseDetails()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Initial hold", "user1@example.com", CancellationToken.None);
            var releasedBy = "user2@example.com";
            var releaseReason = "Litigation concluded";

            // Act
            await _repository.ReleaseAsync(hold.Id, releasedBy, releaseReason, CancellationToken.None);

            // Assert - Query the context directly to verify all fields were updated
            await using var db = await _fixture.Factory.CreateDbContextAsync();
            var releasedHold = await db.LegalHolds.FindAsync(hold.Id);

            Assert.NotNull(releasedHold);
            Assert.Equal(releasedBy, releasedHold.ReleasedBy);
            Assert.NotNull(releasedHold.ReleasedAtUtc);
            Assert.Equal(releaseReason, releasedHold.ReleaseReason);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_CreatesChainOfCustodyEntry()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Hold reason", "analyst@example.com", CancellationToken.None);
            var releasedBy = "manager@example.com";
            var releaseReason = "Investigation complete";

            // Act
            await _repository.ReleaseAsync(hold.Id, releasedBy, releaseReason, CancellationToken.None);

            // Assert - Verify action log entry was created
            var history = await _actionLogRepository.GetHistoryForEntityAsync("MediaItem", mediaItemId, CancellationToken.None);
            var releaseLogEntry = history.FirstOrDefault(e => e.Action == "ReleaseLegalHold");

            Assert.NotNull(releaseLogEntry);
            Assert.Equal(releasedBy, releaseLogEntry.Actor);
            Assert.Equal(mediaItemId, releaseLogEntry.EntityId);
            Assert.Contains(releaseReason, releaseLogEntry.DetailsJson ?? "");
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_ThrowsWhenHoldDoesNotExist()
        {
            // Arrange
            var nonExistentHoldId = Guid.NewGuid();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _repository.ReleaseAsync(nonExistentHoldId, "user@example.com", "Reason", CancellationToken.None)
            );

            Assert.Contains("does not exist", exception.Message);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_ThrowsWhenAlreadyReleased()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Hold", "user1@example.com", CancellationToken.None);

            // Release once
            await _repository.ReleaseAsync(hold.Id, "user2@example.com", "First release", CancellationToken.None);

            // Act & Assert - Try to release again
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _repository.ReleaseAsync(hold.Id, "user3@example.com", "Second release", CancellationToken.None)
            );

            Assert.Contains("already released", exception.Message);
        }

        [Fact]
        public async Task LegalHoldRepository_GetActiveByMediaItemIdsAsync_ReturnsOnlyActiveholds()
        {
            // Arrange
            var mediaItem1 = Guid.NewGuid();
            var mediaItem2 = Guid.NewGuid();
            var mediaItem3 = Guid.NewGuid();

            // Place holds on three items
            var hold1 = await _repository.PlaceAsync(mediaItem1, "Hold 1", "user@example.com", CancellationToken.None);
            var hold2 = await _repository.PlaceAsync(mediaItem2, "Hold 2", "user@example.com", CancellationToken.None);
            var hold3 = await _repository.PlaceAsync(mediaItem3, "Hold 3", "user@example.com", CancellationToken.None);

            // Release hold2
            await _repository.ReleaseAsync(hold2.Id, "user@example.com", "Release reason", CancellationToken.None);

            // Act
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(
                new[] { mediaItem1, mediaItem2, mediaItem3 },
                CancellationToken.None
            );

            // Assert - Only holds 1 and 3 should be returned
            Assert.Equal(2, activeHolds.Count);
            Assert.Contains(activeHolds, h => h.Id == hold1.Id);
            Assert.Contains(activeHolds, h => h.Id == hold3.Id);
            Assert.DoesNotContain(activeHolds, h => h.Id == hold2.Id);
        }

        [Fact]
        public async Task LegalHoldRepository_GetActiveByMediaItemIdsAsync_ReturnsEmptyWhenNoHoldsExist()
        {
            // Arrange
            var mediaItem1 = Guid.NewGuid();
            var mediaItem2 = Guid.NewGuid();

            // Act
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(
                new[] { mediaItem1, mediaItem2 },
                CancellationToken.None
            );

            // Assert
            Assert.Empty(activeHolds);
        }

        [Fact]
        public async Task LegalHoldRepository_GetActiveByMediaItemIdsAsync_IgnoresMediaItemsWithoutHolds()
        {
            // Arrange
            var mediaItemWithHold = Guid.NewGuid();
            var mediaItemWithoutHold = Guid.NewGuid();
            var anotherMediaItemWithoutHold = Guid.NewGuid();

            var hold = await _repository.PlaceAsync(mediaItemWithHold, "Hold reason", "user@example.com", CancellationToken.None);

            // Act
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(
                new[] { mediaItemWithHold, mediaItemWithoutHold, anotherMediaItemWithoutHold },
                CancellationToken.None
            );

            // Assert
            Assert.Single(activeHolds);
            Assert.Equal(hold.Id, activeHolds[0].Id);
            Assert.Equal(mediaItemWithHold, activeHolds[0].MediaItemId);
        }

        [Fact]
        public async Task LegalHoldRepository_GetActiveByMediaItemIdsAsync_WithEmptyMediaItemIdList()
        {
            // Arrange
            var mediaItem = Guid.NewGuid();
            _ = await _repository.PlaceAsync(mediaItem, "Hold", "user@example.com", CancellationToken.None);

            // Act
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(
                new List<Guid>(),
                CancellationToken.None
            );

            // Assert
            Assert.Empty(activeHolds);
        }

        [Fact]
        public async Task LegalHoldRepository_GetActiveByMediaItemIdsAsync_MultipleHoldsPerMediaItem()
        {
            // Arrange
            var mediaItem = Guid.NewGuid();
            var hold1 = await _repository.PlaceAsync(mediaItem, "Hold 1", "user1@example.com", CancellationToken.None);
            var hold2 = await _repository.PlaceAsync(mediaItem, "Hold 2", "user2@example.com", CancellationToken.None);

            // Act
            var activeHolds = await _repository.GetActiveByMediaItemIdsAsync(
                new[] { mediaItem },
                CancellationToken.None
            );

            // Assert
            Assert.Equal(2, activeHolds.Count);
            Assert.Contains(activeHolds, h => h.Id == hold1.Id);
            Assert.Contains(activeHolds, h => h.Id == hold2.Id);
        }

        [Fact]
        public async Task LegalHoldRepository_PlaceAsync_PreservesReasonWithSpecialCharacters()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var reason = "Hold for \"Discovery\" per court order; includes: emails, photos & documents";

            // Act
            var hold = await _repository.PlaceAsync(mediaItemId, reason, "user@example.com", CancellationToken.None);

            // Assert
            Assert.Equal(reason, hold.Reason);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_PreservesReleaseReasonWithSpecialCharacters()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Initial hold", "user1@example.com", CancellationToken.None);
            var releaseReason = "Case \"dismissed\" per court order; no appeal filed within 30 days";

            // Act
            await _repository.ReleaseAsync(hold.Id, "user2@example.com", releaseReason, CancellationToken.None);

            // Assert - Verify release reason was saved correctly
            await using var db = await _fixture.Factory.CreateDbContextAsync();
            var releasedHold = await db.LegalHolds.FindAsync(hold.Id);
            Assert.Equal(releaseReason, releasedHold?.ReleaseReason);
        }

        [Fact]
        public async Task LegalHoldRepository_PlaceAsync_CreatedAtUtcIsRecentTime()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var beforePlace = DateTime.UtcNow;

            // Act
            var hold = await _repository.PlaceAsync(mediaItemId, "Hold", "user@example.com", CancellationToken.None);
            var afterPlace = DateTime.UtcNow;

            // Assert
            Assert.True(hold.CreatedAtUtc >= beforePlace);
            Assert.True(hold.CreatedAtUtc <= afterPlace);
        }

        [Fact]
        public async Task LegalHoldRepository_ReleaseAsync_ReleasedAtUtcIsRecentTime()
        {
            // Arrange
            var mediaItemId = Guid.NewGuid();
            var hold = await _repository.PlaceAsync(mediaItemId, "Hold", "user1@example.com", CancellationToken.None);
            var beforeRelease = DateTime.UtcNow;

            // Act
            await _repository.ReleaseAsync(hold.Id, "user2@example.com", "Release reason", CancellationToken.None);
            var afterRelease = DateTime.UtcNow;

            // Assert - Query to get the released hold
            await using var db = await _fixture.Factory.CreateDbContextAsync();
            var releasedHold = await db.LegalHolds.FindAsync(hold.Id);

            Assert.NotNull(releasedHold?.ReleasedAtUtc);
            Assert.True(releasedHold.ReleasedAtUtc >= beforeRelease);
            Assert.True(releasedHold.ReleasedAtUtc <= afterRelease);
        }
    }
}
