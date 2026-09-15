using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class IntegrityRecordRepositoryTests : RepositoryTestBase
    {
        private IntegrityRecordRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new IntegrityRecordRepository(Fixture.Factory, CreateLogger<IntegrityRecordRepository>());
        }

        #region AddAsync Tests

        [Fact]
        public async Task AddAsync_CreatesNewRecord_WhenValidRecordProvided()
        {
            var mediaItemId = Guid.NewGuid();
            var record = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "abc123def456",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = true,
                FailureReason = null,
                VerifiedBy = "TestUser"
            };

            await _repository.AddAsync(record, CancellationToken.None);

            // Verify record was saved by querying it back
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            Assert.Equal(record.Id, result[0].Id);
            Assert.Equal(mediaItemId, result[0].MediaItemId);
            Assert.Equal("abc123def456", result[0].Sha256Hash);
            Assert.True(result[0].Passed);
        }

        [Fact]
        public async Task AddAsync_CreatesFailedRecord_WhenValidationFails()
        {
            var mediaItemId = Guid.NewGuid();
            var record = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "expected_hash",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = false,
                FailureReason = "Hash mismatch: expected expected_hash, got actual_hash",
                VerifiedBy = "TestValidator"
            };

            await _repository.AddAsync(record, CancellationToken.None);

            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            Assert.False(result[0].Passed);
            Assert.Equal("Hash mismatch: expected expected_hash, got actual_hash", result[0].FailureReason);
        }

        [Fact]
        public async Task AddAsync_AllowsMultipleRecordsForSameMediaItem()
        {
            var mediaItemId = Guid.NewGuid();
            DateTime firstTime = DateTime.UtcNow;
            DateTime secondTime = firstTime.AddMinutes(5);

            // Add first record
            var record1 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash1",
                VerifiedAtUtc = firstTime,
                Passed = true,
                FailureReason = null,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record1, CancellationToken.None);

            // Add second record for same media item
            var record2 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash2",
                VerifiedAtUtc = secondTime,
                Passed = true,
                FailureReason = null,
                VerifiedBy = "User2"
            };
            await _repository.AddAsync(record2, CancellationToken.None);

            // Both records should exist in database
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result); // But GetLatest should only return the newest one
            Assert.Equal(record2.Id, result[0].Id); // Should be the second record
            Assert.Equal(secondTime, result[0].VerifiedAtUtc);
        }

        [Fact]
        public async Task AddAsync_PreservesAllFields_WhenRecordAdded()
        {
            var mediaItemId = Guid.NewGuid();
            var recordId = Guid.NewGuid();
            var verifiedAt = new DateTime(2026, 5, 15, 10, 30, 0, DateTimeKind.Utc);

            var record = new IntegrityRecord
            {
                Id = recordId,
                MediaItemId = mediaItemId,
                Sha256Hash = "a1b2c3d4e5f6",
                VerifiedAtUtc = verifiedAt,
                Passed = false,
                FailureReason = "Corrupted file detected",
                VerifiedBy = "ForensicsBot"
            };

            await _repository.AddAsync(record, CancellationToken.None);

            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            IntegrityRecord retrieved = result[0];
            Assert.Equal(recordId, retrieved.Id);
            Assert.Equal(mediaItemId, retrieved.MediaItemId);
            Assert.Equal("a1b2c3d4e5f6", retrieved.Sha256Hash);
            Assert.Equal(verifiedAt, retrieved.VerifiedAtUtc);
            Assert.False(retrieved.Passed);
            Assert.Equal("Corrupted file detected", retrieved.FailureReason);
            Assert.Equal("ForensicsBot", retrieved.VerifiedBy);
        }

        #endregion

        #region GetLatestByMediaItemIdsAsync Tests

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsLatestRecord_WhenMultipleRecordsExist()
        {
            var mediaItemId = Guid.NewGuid();
            DateTime baseTime = DateTime.UtcNow;

            // Add three records for same media item at different times
            var record1 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash1",
                VerifiedAtUtc = baseTime.AddHours(-2),
                Passed = true,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record1, CancellationToken.None);

            var record2 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash2",
                VerifiedAtUtc = baseTime.AddHours(-1),
                Passed = false,
                FailureReason = "Initial failure",
                VerifiedBy = "User2"
            };
            await _repository.AddAsync(record2, CancellationToken.None);

            var record3 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash3",
                VerifiedAtUtc = baseTime,
                Passed = true,
                VerifiedBy = "User3"
            };
            await _repository.AddAsync(record3, CancellationToken.None);

            // Should return only the latest
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            Assert.Equal(record3.Id, result[0].Id);
            Assert.Equal("hash3", result[0].Sha256Hash);
            Assert.True(result[0].Passed);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsEmptyList_WhenNoRecordsExist()
        {
            var mediaItemId = Guid.NewGuid();

            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsMultipleLatestRecords_WhenMultipleMediaItemsQueried()
        {
            var mediaItemId1 = Guid.NewGuid();
            var mediaItemId2 = Guid.NewGuid();
            var mediaItemId3 = Guid.NewGuid();
            DateTime baseTime = DateTime.UtcNow;

            // Add records for first media item
            var record1 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId1,
                Sha256Hash = "hash1a",
                VerifiedAtUtc = baseTime.AddMinutes(-10),
                Passed = true,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record1, CancellationToken.None);

            var record1b = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId1,
                Sha256Hash = "hash1b",
                VerifiedAtUtc = baseTime,
                Passed = true,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record1b, CancellationToken.None);

            // Add records for second media item
            var record2 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId2,
                Sha256Hash = "hash2",
                VerifiedAtUtc = baseTime.AddMinutes(-5),
                Passed = false,
                FailureReason = "Corrupted",
                VerifiedBy = "User2"
            };
            await _repository.AddAsync(record2, CancellationToken.None);

            // Add record for third media item
            var record3 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId3,
                Sha256Hash = "hash3",
                VerifiedAtUtc = baseTime.AddMinutes(-20),
                Passed = true,
                VerifiedBy = "User3"
            };
            await _repository.AddAsync(record3, CancellationToken.None);

            // Query all three
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(
                new[] { mediaItemId1, mediaItemId2, mediaItemId3 },
                CancellationToken.None);

            Assert.Equal(3, result.Count);

            // Verify we got the latest for each
            var resultDict = result.ToDictionary(r => r.MediaItemId);
            Assert.Equal(record1b.Id, resultDict[mediaItemId1].Id);
            Assert.Equal(record2.Id, resultDict[mediaItemId2].Id);
            Assert.Equal(record3.Id, resultDict[mediaItemId3].Id);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_SkipsMediaItemsWithNoRecords_WhenMixed()
        {
            var mediaItemIdWithRecord = Guid.NewGuid();
            var mediaItemIdWithoutRecord = Guid.NewGuid();

            // Add record only for first media item
            var record = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemIdWithRecord,
                Sha256Hash = "hash1",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = true,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record, CancellationToken.None);

            // Query both, but second has no records
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(
                new[] { mediaItemIdWithRecord, mediaItemIdWithoutRecord },
                CancellationToken.None);

            _ = Assert.Single(result);
            Assert.Equal(mediaItemIdWithRecord, result[0].MediaItemId);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsRecords_WithEmptyMediaItemIdList()
        {
            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(
                Enumerable.Empty<Guid>(),
                CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsLatestByTimestamp_RegardlessOfInsertionOrder()
        {
            var mediaItemId = Guid.NewGuid();
            DateTime baseTime = DateTime.UtcNow;

            // Insert in non-chronological order
            var record3 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash3",
                VerifiedAtUtc = baseTime.AddHours(2),
                Passed = true,
                VerifiedBy = "User3"
            };
            await _repository.AddAsync(record3, CancellationToken.None);

            var record1 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash1",
                VerifiedAtUtc = baseTime,
                Passed = true,
                VerifiedBy = "User1"
            };
            await _repository.AddAsync(record1, CancellationToken.None);

            var record2 = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "hash2",
                VerifiedAtUtc = baseTime.AddHours(1),
                Passed = true,
                VerifiedBy = "User2"
            };
            await _repository.AddAsync(record2, CancellationToken.None);

            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            Assert.Equal(record3.Id, result[0].Id);
            Assert.Equal("hash3", result[0].Sha256Hash);
        }

        [Fact]
        public async Task GetLatestByMediaItemIdsAsync_ReturnsSingleRecord_WhenOnlyOneRecordExists()
        {
            var mediaItemId = Guid.NewGuid();
            var record = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItemId,
                Sha256Hash = "onlyhash",
                VerifiedAtUtc = DateTime.UtcNow,
                Passed = true,
                VerifiedBy = "Verifier"
            };

            await _repository.AddAsync(record, CancellationToken.None);

            IReadOnlyList<IntegrityRecord> result = await _repository.GetLatestByMediaItemIdsAsync(new[] { mediaItemId }, CancellationToken.None);
            _ = Assert.Single(result);
            Assert.Equal(record.Id, result[0].Id);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task Workflow_AddMultipleRecordsAndRetrieveLatest()
        {
            Guid[] mediaItems = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            DateTime baseTime = DateTime.UtcNow;

            // Add initial records
            foreach (Guid mediaItemId in mediaItems)
            {
                var record = new IntegrityRecord
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItemId,
                    Sha256Hash = $"hash_initial_{mediaItemId:N}",
                    VerifiedAtUtc = baseTime,
                    Passed = true,
                    VerifiedBy = "InitialVerifier"
                };
                await _repository.AddAsync(record, CancellationToken.None);
            }

            // Verify all records exist
            IReadOnlyList<IntegrityRecord> initialResult = await _repository.GetLatestByMediaItemIdsAsync(mediaItems, CancellationToken.None);
            Assert.Equal(3, initialResult.Count);
            Assert.All(initialResult, r => Assert.True(r.Passed));

            // Re-verify first media item and it fails
            var recheck = new IntegrityRecord
            {
                Id = Guid.NewGuid(),
                MediaItemId = mediaItems[0],
                Sha256Hash = $"hash_recheck_{mediaItems[0]:N}",
                VerifiedAtUtc = baseTime.AddMinutes(5),
                Passed = false,
                FailureReason = "Recheck detected corruption",
                VerifiedBy = "ReVerifier"
            };
            await _repository.AddAsync(recheck, CancellationToken.None);

            // Get latest - first item should now show failure
            IReadOnlyList<IntegrityRecord> updated = await _repository.GetLatestByMediaItemIdsAsync(mediaItems, CancellationToken.None);
            Assert.Equal(3, updated.Count);

            IntegrityRecord? firstItemResult = updated.FirstOrDefault(r => r.MediaItemId == mediaItems[0]);
            Assert.NotNull(firstItemResult);
            Assert.False(firstItemResult.Passed);
            Assert.Equal("Recheck detected corruption", firstItemResult.FailureReason);

            // Others should still be passing
            var otherResults = updated.Where(r => r.MediaItemId != mediaItems[0]).ToList();
            Assert.All(otherResults, r => Assert.True(r.Passed));
        }

        #endregion
    }
}
