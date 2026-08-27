using Microsoft.EntityFrameworkCore;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for database integrity constraints and operations during batch downloads and watermark operations.</summary>
    public class DownloadIntegrityTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private VideoForensicsDbContext _dbContext = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _dbContext = _fixture.Factory.CreateDbContext();
        }

        public async ValueTask DisposeAsync()
        {
            await _dbContext.DisposeAsync();
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task RecordDownloadEvent_WithDuplicateProviderEventId_ThrowsUniqueConstraintViolation()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            await _dbContext.SaveChangesAsync();

            var downloadEvent1 = TestDataBuilder.BuildDownloadEvent(device.Id, "event-123", true);
            _dbContext.DownloadEvents.Add(downloadEvent1);
            await _dbContext.SaveChangesAsync();

            // Act: Try to insert same ProviderEventId for same device
            var downloadEvent2 = TestDataBuilder.BuildDownloadEvent(device.Id, "event-123", true);

            // Assert: Should throw due to unique constraint
            _dbContext.DownloadEvents.Add(downloadEvent2);
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await _dbContext.SaveChangesAsync();
            });
        }

        [Fact]
        public async Task MediaItem_CanHaveOptionalDownloadEventId()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            await _dbContext.SaveChangesAsync();

            // Act: MediaItem can be created without a DownloadEvent (FK is optional)
            var mediaItem = TestDataBuilder.BuildMediaItem(
                deviceId: device.Id,
                downloadEventId: null  // Optional FK
            );

            _dbContext.MediaItems.Add(mediaItem);
            await _dbContext.SaveChangesAsync();

            // Assert: MediaItem persisted successfully
            var retrieved = await _dbContext.MediaItems.FindAsync(mediaItem.Id);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved.DownloadEventId);
        }

        [Fact]
        public async Task WatermarkAdvancement_IsMonotonicallyIncreasing()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            device.LastSuccessfulPullAtUtc = DateTime.UtcNow.AddDays(-7);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            await _dbContext.SaveChangesAsync();

            var initialWatermark = device.LastSuccessfulPullAtUtc.Value;

            // Act: Simulate advancing watermark with three downloads
            var timestamps = new[]
            {
                DateTime.UtcNow.AddHours(-3),
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1)
            };

            foreach (var timestamp in timestamps)
            {
                var downloadEvent = TestDataBuilder.BuildDownloadEvent(device.Id, $"event-{timestamp:O}", true);
                downloadEvent.EventOccurredAtUtc = timestamp;
                downloadEvent.DownloadCompletedUtc = DateTime.UtcNow;

                _dbContext.DownloadEvents.Add(downloadEvent);

                device.LastSuccessfulPullAtUtc = timestamp;
                _dbContext.Devices.Update(device);
                await _dbContext.SaveChangesAsync();
            }

            // Assert: Watermark is monotonically increasing
            var finalDevice = await _dbContext.Devices.FindAsync(device.Id);
            Assert.NotNull(finalDevice);
            Assert.True(finalDevice.LastSuccessfulPullAtUtc > initialWatermark);
            Assert.Equal(timestamps.Max(), finalDevice.LastSuccessfulPullAtUtc);
        }

        [Fact]
        public async Task IntegrityRecord_CreatedPerVerification_NoOrphanedRecords()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var downloadEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "evt-123", true);
            var mediaItem = TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            _dbContext.DownloadEvents.Add(downloadEvent);
            _dbContext.MediaItems.Add(mediaItem);
            await _dbContext.SaveChangesAsync();

            // Act: Add integrity records with specific timestamps
            var now = DateTime.UtcNow;
            var records = new[]
            {
                new IntegrityRecord
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItem.Id,
                    Sha256Hash = mediaItem.Sha256Hash,
                    VerifiedAtUtc = now,
                    Passed = true,
                    VerifiedBy = "test"
                },
                new IntegrityRecord
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItem.Id,
                    Sha256Hash = mediaItem.Sha256Hash,
                    VerifiedAtUtc = now.AddHours(1),
                    Passed = true,
                    VerifiedBy = "test"
                }
            };

            foreach (var record in records)
                _dbContext.IntegrityRecords.Add(record);
            await _dbContext.SaveChangesAsync();

            // Assert: All records exist and reference valid MediaItem
            var allRecords = await _dbContext.IntegrityRecords
                .Where(r => r.MediaItemId == mediaItem.Id)
                .ToListAsync();

            Assert.Equal(2, allRecords.Count);
            Assert.All(allRecords, r =>
            {
                Assert.True(r.VerifiedAtUtc >= now);
                Assert.True(r.VerifiedAtUtc <= now.AddHours(2));
            });
        }

        [Fact]
        public async Task SoftDelete_MediaItem_PreservesIntegrityHistory()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var downloadEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "evt-123", true);
            var mediaItem = TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            _dbContext.DownloadEvents.Add(downloadEvent);
            _dbContext.MediaItems.Add(mediaItem);
            await _dbContext.SaveChangesAsync();

            // Act: Soft-delete the media item
            mediaItem.IsPurged = true;
            mediaItem.PurgedAtUtc = DateTime.UtcNow;
            mediaItem.PurgeReason = "Retention policy";
            _dbContext.MediaItems.Update(mediaItem);
            await _dbContext.SaveChangesAsync();

            // Assert: MediaItem still exists but marked purged; DownloadEvent preserved
            var purgedItem = await _dbContext.MediaItems.FindAsync(mediaItem.Id);
            Assert.NotNull(purgedItem);
            Assert.True(purgedItem.IsPurged);
            Assert.NotNull(purgedItem.PurgedAtUtc);
            Assert.Equal("Retention policy", purgedItem.PurgeReason);

            var downloadEventStillExists = await _dbContext.DownloadEvents.FindAsync(downloadEvent.Id);
            Assert.NotNull(downloadEventStillExists);
        }

        [Fact]
        public async Task MultipleMediaItems_SameDownloadEvent_AllRoundTrip()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var downloadEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "batch-evt-001", true);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            _dbContext.DownloadEvents.Add(downloadEvent);
            await _dbContext.SaveChangesAsync();

            // Act: Create multiple media items for the same download event
            var mediaItems = new[]
            {
                TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id, "video_001.mp4", "hash_001"),
                TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id, "video_002.mp4", "hash_002"),
                TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id, "video_003.mp4", "hash_003")
            };

            foreach (var item in mediaItems)
                _dbContext.MediaItems.Add(item);
            await _dbContext.SaveChangesAsync();

            // Assert: All items exist and link to the same download event
            var retrievedItems = await _dbContext.MediaItems
                .Where(m => m.DownloadEventId == downloadEvent.Id)
                .ToListAsync();

            Assert.Equal(3, retrievedItems.Count);
            Assert.All(retrievedItems, item =>
            {
                Assert.Equal(device.Id, item.DeviceId);
                Assert.Equal(downloadEvent.Id, item.DownloadEventId);
            });
        }

        [Fact]
        public async Task DownloadEventIntegrity_PartialFailure_DoesNotCorruptSuccessfulRecords()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var successEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "evt-success", true);
            var failureEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "evt-failure", false);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            _dbContext.DownloadEvents.Add(successEvent);
            _dbContext.DownloadEvents.Add(failureEvent);
            await _dbContext.SaveChangesAsync();

            var successMediaItem = TestDataBuilder.BuildMediaItem(device.Id, successEvent.Id);
            _dbContext.MediaItems.Add(successMediaItem);
            await _dbContext.SaveChangesAsync();

            // Act: Attempt to add media item for failed event (should still work - FK not strictly enforced at insert)
            var failureMediaItem = TestDataBuilder.BuildMediaItem(device.Id, failureEvent.Id);
            _dbContext.MediaItems.Add(failureMediaItem);
            await _dbContext.SaveChangesAsync();

            // Assert: Both events and their media items exist; success is not corrupted
            var retrievedSuccess = await _dbContext.DownloadEvents.FindAsync(successEvent.Id);
            var retrievedFailure = await _dbContext.DownloadEvents.FindAsync(failureEvent.Id);

            Assert.NotNull(retrievedSuccess);
            Assert.True(retrievedSuccess.Success);

            Assert.NotNull(retrievedFailure);
            Assert.False(retrievedFailure.Success);

            var allMediaItems = await _dbContext.MediaItems
                .Where(m => m.DeviceId == device.Id)
                .ToListAsync();
            Assert.Equal(2, allMediaItems.Count);
        }

        [Fact]
        public async Task IntegrityVerification_FailureRecorded_WithReason()
        {
            // Arrange
            var location = TestDataBuilder.BuildLocation();
            var device = TestDataBuilder.BuildDevice(location.Id);
            var downloadEvent = TestDataBuilder.BuildDownloadEvent(device.Id, "verify-evt", true);
            var mediaItem = TestDataBuilder.BuildMediaItem(device.Id, downloadEvent.Id);

            _dbContext.Locations.Add(location);
            _dbContext.Devices.Add(device);
            _dbContext.DownloadEvents.Add(downloadEvent);
            _dbContext.MediaItems.Add(mediaItem);
            await _dbContext.SaveChangesAsync();

            // Act: Record a failed integrity verification
            var failureRecord = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItem.Id,
                Sha256Hash = "corrupted_hash_xyz",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = false,
                FailureReason = "Hash mismatch: expected abc123, got xyz789",
                VerifiedBy = "automated_check"
            };

            _dbContext.IntegrityRecords.Add(failureRecord);
            await _dbContext.SaveChangesAsync();

            // Assert: Failure is recorded with reason
            var retrieved = await _dbContext.IntegrityRecords.FindAsync(failureRecord.Id);
            Assert.NotNull(retrieved);
            Assert.False(retrieved.Passed);
            Assert.NotNull(retrieved.FailureReason);
            Assert.Contains("Hash mismatch", retrieved.FailureReason);
        }
    }
}
