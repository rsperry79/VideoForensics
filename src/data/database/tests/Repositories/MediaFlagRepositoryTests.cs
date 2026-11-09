using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>Tests for MediaFlagRepository: idempotent writes, reconcile, SQL paging, keyset neighbours, compare-and-set review and audit.</summary>
    public class MediaFlagRepositoryTests : IAsyncLifetime
    {
        private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly CancellationToken Ct = CancellationToken.None;

        private SqliteInMemoryFixture _fixture = null!;
        private MediaFlagRepository _repository = null!;
        private ActionLogRepository _actionLog = null!;
        private MediaItemRepository _media = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var unitOfWork = new UnitOfWork(_fixture.Factory, new TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>());
            _actionLog = new ActionLogRepository(unitOfWork, NullLogger<ActionLogRepository>.Instance);
            _repository = new MediaFlagRepository(unitOfWork, NullLogger<MediaFlagRepository>.Instance);
            _media = new MediaItemRepository(unitOfWork, NullLogger<MediaItemRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private async Task<MediaItem> AddMediaAsync(Guid? deviceId = null)
        {
            MediaItem item = TestDataBuilder.BuildMediaItem(deviceId: deviceId ?? Guid.NewGuid());
            await _media.AddAsync(item, Ct);
            return item;
        }

        private static MediaFlag Raised(Guid deviceId, Guid? mediaItemId, MediaFlagType type = MediaFlagType.BitrateCollapse, int minutes = 0, MediaFlagSeverity severity = MediaFlagSeverity.Warn)
        {
            return new MediaFlag
            {
                FlagType = type,
                DeviceId = deviceId,
                MediaItemId = mediaItemId,
                WindowStartUtc = T0.AddMinutes(minutes),
                WindowEndUtc = T0.AddMinutes(minutes + 1),
                RaisedAtUtc = T0.AddMinutes(minutes),
                Severity = severity,
            };
        }

        private async Task<MediaFlag> RaiseAsync(Guid deviceId, int minutes, MediaFlagType type = MediaFlagType.RssiDeviation)
        {
            return await _repository.UpsertRaisedAsync(Raised(deviceId, null, type, minutes), Ct);
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_SameKeyTwice_StoresOneRow()
        {
            MediaItem item = await AddMediaAsync();

            MediaFlag first = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id), Ct);
            MediaFlag second = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id), Ct);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter(), Ct));
            Assert.Equal(MediaFlagStatus.Unreviewed, second.Status);
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_UnreviewedRow_UpdatesWindowAndSeverity()
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag first = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, severity: MediaFlagSeverity.Info), Ct);

            MediaFlag changed = Raised(item.DeviceId, item.Id, severity: MediaFlagSeverity.Alert);
            changed.WindowEndUtc = T0.AddMinutes(9);
            MediaFlag second = await _repository.UpsertRaisedAsync(changed, Ct);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(MediaFlagSeverity.Alert, second.Severity);
            Assert.Equal(T0.AddMinutes(9), second.WindowEndUtc);
            Assert.True(second.RowVersion > first.RowVersion);
        }

        [Theory]
        [InlineData(MediaFlagStatus.Kept)]
        [InlineData(MediaFlagStatus.Dismissed)]
        public async Task MediaFlagRepository_UpsertRaisedAsync_ReviewedRow_IsNotTouched(MediaFlagStatus reviewed)
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag first = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, severity: MediaFlagSeverity.Info), Ct);
            _ = await _repository.UpdateStatusAsync(first.Id, MediaFlagStatus.Unreviewed, reviewed, "checked", "alice", Ct);

            MediaFlag again = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, severity: MediaFlagSeverity.Alert), Ct);

            Assert.Equal(first.Id, again.Id);
            Assert.Equal(reviewed, again.Status);
            Assert.Equal(MediaFlagSeverity.Info, again.Severity);
            Assert.Equal("alice", again.ReviewedBy);
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_NoMediaSameDeviceTypeStart_StoresOneRow()
        {
            var device = Guid.NewGuid();

            MediaFlag a = await RaiseAsync(device, 0);
            MediaFlag b = await RaiseAsync(device, 0);

            Assert.Equal(a.Id, b.Id);
            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter(), Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_NoMediaAndEmptyDevice_Throws()
        {
            MediaFlag flag = Raised(Guid.Empty, null, MediaFlagType.RssiDeviation);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpsertRaisedAsync(flag, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_WindowEndBeforeStart_Throws()
        {
            MediaFlag flag = Raised(Guid.NewGuid(), null, MediaFlagType.RssiDeviation);
            flag.WindowEndUtc = flag.WindowStartUtc.AddSeconds(-1);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpsertRaisedAsync(flag, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UniqueIndexes_DuplicateMediaAndTypeRejectedByDatabase()
        {
            MediaItem item = await AddMediaAsync();
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(Ct);
            _ = db.MediaFlags.Add(Raised(item.DeviceId, item.Id));
            _ = await db.SaveChangesAsync(Ct);

            await using VideoForensicsDbContext db2 = await _fixture.Factory.CreateDbContextAsync(Ct);
            MediaFlag duplicate = Raised(item.DeviceId, item.Id, minutes: 5);
            _ = db2.MediaFlags.Add(duplicate);

            _ = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UniqueIndexes_DuplicateDeviceTypeStartRejectedByDatabase()
        {
            var device = Guid.NewGuid();
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(Ct);
            _ = db.MediaFlags.Add(Raised(device, null, MediaFlagType.RssiDeviation));
            _ = db.MediaFlags.Add(Raised(device, null, MediaFlagType.RssiDeviation));

            _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_RowVersion_ConcurrentChange_ThrowsConcurrencyException()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);
            await using VideoForensicsDbContext a = await _fixture.Factory.CreateDbContextAsync(Ct);
            MediaFlag loaded = await a.MediaFlags.SingleAsync(f => f.Id == flag.Id, Ct);

            await using (VideoForensicsDbContext b = await _fixture.Factory.CreateDbContextAsync(Ct))
            {
                _ = await b.MediaFlags.Where(f => f.Id == flag.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.RowVersion, f => f.RowVersion + 1), Ct);
            }

            loaded.Status = MediaFlagStatus.Kept;
            _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => a.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_ReconcileForMediaAsync_RemovesStaleUnreviewedKeepsReviewed()
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag stale = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, MediaFlagType.BitrateCollapse), Ct);
            MediaFlag kept = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, MediaFlagType.ClippedAudio), Ct);
            MediaFlag dismissed = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, MediaFlagType.AudioDropout), Ct);
            MediaFlag stillRaised = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id, MediaFlagType.UnusualLighting), Ct);
            _ = await _repository.UpdateStatusAsync(kept.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", Ct);
            _ = await _repository.UpdateStatusAsync(dismissed.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "noise", "alice", Ct);

            int removed = await _repository.ReconcileForMediaAsync(item.Id, new HashSet<MediaFlagType> { MediaFlagType.UnusualLighting }, Ct);

            Assert.Equal(1, removed);
            Assert.Null(await _repository.GetAsync(stale.Id, Ct));
            Assert.NotNull(await _repository.GetAsync(kept.Id, Ct));
            Assert.NotNull(await _repository.GetAsync(dismissed.Id, Ct));
            Assert.NotNull(await _repository.GetAsync(stillRaised.Id, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_ReconcileForMediaAsync_OtherMediaUntouched()
        {
            MediaItem a = await AddMediaAsync();
            MediaItem b = await AddMediaAsync();
            _ = await _repository.UpsertRaisedAsync(Raised(a.DeviceId, a.Id), Ct);
            MediaFlag other = await _repository.UpsertRaisedAsync(Raised(b.DeviceId, b.Id), Ct);

            _ = await _repository.ReconcileForMediaAsync(a.Id, new HashSet<MediaFlagType>(), Ct);

            Assert.NotNull(await _repository.GetAsync(other.Id, Ct));
            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter(), Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_DeleteMediaItem_KeepsFlagWithNullMediaItem()
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id), Ct);

            await _media.DeleteAsync(item.Id, Ct);

            MediaFlag? after = await _repository.GetAsync(flag.Id, Ct);
            Assert.NotNull(after);
            Assert.Null(after.MediaItemId);
            Assert.Equal(item.DeviceId, after.DeviceId);
            Assert.Equal(flag.WindowStartUtc, after.WindowStartUtc);
        }

        [Fact]
        public async Task MediaFlagRepository_PurgeMediaItem_KeepsFlagLinked()
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id), Ct);

            item.IsPurged = true;
            await _media.UpdateAsync(item, Ct);

            MediaFlag? after = await _repository.GetAsync(flag.Id, Ct);
            Assert.NotNull(after);
            Assert.Equal(item.Id, after.MediaItemId);
        }

        [Fact]
        public async Task MediaFlagRepository_ListPageAsync_FiltersAndPagesInSql_NewestFirst()
        {
            var cam1 = Guid.NewGuid();
            var cam2 = Guid.NewGuid();
            for (int i = 0; i < 5; i++)
            {
                _ = await RaiseAsync(cam1, i);
            }

            MediaFlag other = await RaiseAsync(cam2, 10);
            MediaFlag audio = await _repository.UpsertRaisedAsync(Raised(cam1, null, MediaFlagType.AudioDropout, 20), Ct);
            _ = await _repository.UpdateStatusAsync(other.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", Ct);

            var filter = new MediaFlagFilter { DeviceIds = new[] { cam1 }, Types = new[] { MediaFlagType.RssiDeviation }, Statuses = new[] { MediaFlagStatus.Unreviewed } };
            MediaFlagPage page1 = await _repository.ListPageAsync(filter, skip: 0, take: 2, Ct);
            MediaFlagPage page3 = await _repository.ListPageAsync(filter, skip: 4, take: 2, Ct);

            Assert.Equal(5, page1.TotalCount);
            Assert.Equal(new[] { T0.AddMinutes(4), T0.AddMinutes(3) }, page1.Items.Select(f => f.RaisedAtUtc).ToArray());
            _ = Assert.Single(page3.Items);
            Assert.DoesNotContain(page1.Items, f => f.Id == audio.Id);
        }

        [Fact]
        public async Task MediaFlagRepository_ListPageAsync_DateRange_FiltersOnWindowStart()
        {
            var cam = Guid.NewGuid();
            for (int i = 0; i < 6; i++)
            {
                _ = await RaiseAsync(cam, i);
            }

            var filter = new MediaFlagFilter { FromUtc = T0.AddMinutes(2), ToUtc = T0.AddMinutes(4) };
            MediaFlagPage page = await _repository.ListPageAsync(filter, 0, 50, Ct);

            Assert.Equal(3, page.TotalCount);
            Assert.Equal(3, await _repository.CountAsync(filter, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_CountAsync_CountsByStatus()
        {
            var cam = Guid.NewGuid();
            MediaFlag a = await RaiseAsync(cam, 0);
            _ = await RaiseAsync(cam, 1);
            _ = await _repository.UpdateStatusAsync(a.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "x", "alice", Ct);

            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter { Statuses = new[] { MediaFlagStatus.Unreviewed } }, Ct));
            Assert.Equal(1, await _repository.CountAsync(new MediaFlagFilter { Statuses = new[] { MediaFlagStatus.Dismissed } }, Ct));
            Assert.Equal(2, await _repository.CountAsync(new MediaFlagFilter(), Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_GetNeighbourAsync_StartMiddleEnd()
        {
            var cam = Guid.NewGuid();
            MediaFlag oldest = await RaiseAsync(cam, 0);
            MediaFlag middle = await RaiseAsync(cam, 1);
            MediaFlag newest = await RaiseAsync(cam, 2);
            var filter = new MediaFlagFilter();

            // List order is newest first: Next walks toward older flags, Previous toward newer.
            Assert.Equal(middle.Id, (await _repository.GetNeighbourAsync(newest.Id, filter, MediaFlagNeighbour.Next, Ct))?.Id);
            Assert.Equal(oldest.Id, (await _repository.GetNeighbourAsync(middle.Id, filter, MediaFlagNeighbour.Next, Ct))?.Id);
            Assert.Null(await _repository.GetNeighbourAsync(oldest.Id, filter, MediaFlagNeighbour.Next, Ct));
            Assert.Null(await _repository.GetNeighbourAsync(newest.Id, filter, MediaFlagNeighbour.Previous, Ct));
            Assert.Equal(newest.Id, (await _repository.GetNeighbourAsync(middle.Id, filter, MediaFlagNeighbour.Previous, Ct))?.Id);
        }

        [Fact]
        public async Task MediaFlagRepository_GetNeighbourAsync_SameRaisedAt_TieBreaksById()
        {
            var cam = Guid.NewGuid();
            for (int i = 0; i < 3; i++)
            {
                MediaFlag f = Raised(cam, null, MediaFlagType.RssiDeviation, i);
                f.RaisedAtUtc = T0;
                _ = await _repository.UpsertRaisedAsync(f, Ct);
            }

            MediaFlagPage page = await _repository.ListPageAsync(new MediaFlagFilter(), 0, 10, Ct);
            var seen = new List<Guid> { page.Items[0].Id };
            MediaFlag? next = await _repository.GetNeighbourAsync(seen[0], new MediaFlagFilter(), MediaFlagNeighbour.Next, Ct);
            while (next is not null)
            {
                seen.Add(next.Id);
                next = await _repository.GetNeighbourAsync(next.Id, new MediaFlagFilter(), MediaFlagNeighbour.Next, Ct);
            }

            Assert.Equal(page.Items.Select(f => f.Id).ToArray(), seen.ToArray());
        }

        [Fact]
        public async Task MediaFlagRepository_GetNeighbourAsync_RespectsFilter()
        {
            var cam = Guid.NewGuid();
            MediaFlag a = await RaiseAsync(cam, 0);
            MediaFlag skipped = await _repository.UpsertRaisedAsync(Raised(cam, null, MediaFlagType.AudioDropout, 1), Ct);
            MediaFlag c = await RaiseAsync(cam, 2);

            MediaFlag? next = await _repository.GetNeighbourAsync(c.Id, new MediaFlagFilter { Types = new[] { MediaFlagType.RssiDeviation } }, MediaFlagNeighbour.Next, Ct);

            Assert.Equal(a.Id, next?.Id);
            Assert.NotEqual(skipped.Id, next?.Id);
        }

        [Fact]
        public async Task MediaFlagRepository_GetNeighbourAsync_CurrentNoLongerMatchesFilter_ReturnsNeighbourThatMatches()
        {
            var cam = Guid.NewGuid();
            MediaFlag newest = await RaiseAsync(cam, 2);
            MediaFlag current = await RaiseAsync(cam, 1);
            MediaFlag oldest = await RaiseAsync(cam, 0);
            _ = await _repository.UpdateStatusAsync(current.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "noise", "alice", Ct);
            var unreviewed = new MediaFlagFilter { Statuses = new[] { MediaFlagStatus.Unreviewed } };

            // The current flag is Dismissed, so it is outside the filter, but its position still anchors the walk.
            Assert.Equal(oldest.Id, (await _repository.GetNeighbourAsync(current.Id, unreviewed, MediaFlagNeighbour.Next, Ct))?.Id);
            Assert.Equal(newest.Id, (await _repository.GetNeighbourAsync(current.Id, unreviewed, MediaFlagNeighbour.Previous, Ct))?.Id);
        }

        [Fact]
        public async Task MediaFlagRepository_GetNeighbourAsync_CurrentMatchesFilter_ReturnsNeighbourThatMatches()
        {
            var cam = Guid.NewGuid();
            MediaFlag newest = await RaiseAsync(cam, 2);
            MediaFlag current = await RaiseAsync(cam, 1);
            MediaFlag oldest = await RaiseAsync(cam, 0);
            var unreviewed = new MediaFlagFilter { Statuses = new[] { MediaFlagStatus.Unreviewed } };

            Assert.Equal(oldest.Id, (await _repository.GetNeighbourAsync(current.Id, unreviewed, MediaFlagNeighbour.Next, Ct))?.Id);
            Assert.Equal(newest.Id, (await _repository.GetNeighbourAsync(current.Id, unreviewed, MediaFlagNeighbour.Previous, Ct))?.Id);
        }

        [Fact]
        public async Task MediaFlagRepository_GetPosition_ReturnsOneBasedPositionInListOrder()
        {
            var cam = Guid.NewGuid();
            MediaFlag oldest = await RaiseAsync(cam, 0);
            MediaFlag middle = await RaiseAsync(cam, 1);
            MediaFlag newest = await RaiseAsync(cam, 2);
            MediaFlag excluded = await _repository.UpsertRaisedAsync(Raised(cam, null, MediaFlagType.AudioDropout, 3), Ct);
            var filter = new MediaFlagFilter { Types = new[] { MediaFlagType.RssiDeviation } };

            // List order is newest first, so the newest flag is position 1 and the oldest is position 3.
            Assert.Equal(1, await _repository.GetPositionAsync(newest.Id, filter, Ct));
            Assert.Equal(2, await _repository.GetPositionAsync(middle.Id, filter, Ct));
            Assert.Equal(3, await _repository.GetPositionAsync(oldest.Id, filter, Ct));
            Assert.Null(await _repository.GetPositionAsync(excluded.Id, filter, Ct));
            Assert.Null(await _repository.GetPositionAsync(Guid.NewGuid(), filter, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_GetPosition_TieOnRaisedAt_UsesIdTieBreak()
        {
            var cam = Guid.NewGuid();
            for (int i = 0; i < 3; i++)
            {
                MediaFlag f = Raised(cam, null, MediaFlagType.RssiDeviation, i);
                f.RaisedAtUtc = T0;
                _ = await _repository.UpsertRaisedAsync(f, Ct);
            }

            // The position must agree with the list's own order, whatever the tie-break on Id is.
            MediaFlagPage page = await _repository.ListPageAsync(new MediaFlagFilter(), 0, 10, Ct);
            for (int i = 0; i < page.Items.Count; i++)
            {
                Assert.Equal(i + 1, await _repository.GetPositionAsync(page.Items[i].Id, new MediaFlagFilter(), Ct));
            }
        }

        [Fact]
        public async Task MediaFlagRepository_UpdateStatusAsync_Keep_SetsReviewFieldsAndAudits()
        {
            MediaItem item = await AddMediaAsync();
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(item.DeviceId, item.Id), Ct);

            MediaFlag updated = await _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, "looks real", "alice", Ct);

            Assert.Equal(MediaFlagStatus.Kept, updated.Status);
            Assert.Equal("alice", updated.ReviewedBy);
            _ = Assert.NotNull(updated.ReviewedAtUtc);
            Assert.Equal("looks real", updated.ReviewNote);
            Assert.True(updated.RowVersion > flag.RowVersion);

            IReadOnlyList<ActionLogEntry> log = await _actionLog.GetHistoryForEntityAsync("MediaFlag", flag.Id, Ct);
            ActionLogEntry entry = Assert.Single(log);
            Assert.Equal("KeepMediaFlag", entry.Action);
            Assert.Equal("alice", entry.Actor);
            Assert.Contains(flag.Id.ToString(), entry.DetailsJson);
            Assert.Contains(item.Id.ToString(), entry.DetailsJson);
            Assert.Contains("Unreviewed", entry.DetailsJson);
            Assert.Contains("Kept", entry.DetailsJson);
            Assert.Contains("looks real", entry.DetailsJson);
            Assert.True((await _actionLog.VerifyChainIntegrityAsync(Ct)).IsValid);
        }

        [Fact]
        public async Task MediaFlagRepository_UpdateStatusAsync_StaleExpectedStatus_ThrowsConflictAndWritesNoAudit()
        {
            var cam = Guid.NewGuid();
            MediaFlag flag = await RaiseAsync(cam, 0);
            _ = await _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", Ct);

            FlagStatusConflictException ex = await Assert.ThrowsAsync<FlagStatusConflictException>(
                () => _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "no", "bob", Ct));

            Assert.Equal(MediaFlagStatus.Kept, ex.ActualStatus);
            Assert.Equal("alice", ex.ReviewedBy);
            Assert.Equal(MediaFlagStatus.Kept, (await _repository.GetAsync(flag.Id, Ct))!.Status);
            _ = Assert.Single(await _actionLog.GetHistoryForEntityAsync("MediaFlag", flag.Id, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpdateStatusAsync_UnknownFlag_Throws()
        {
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _repository.UpdateStatusAsync(Guid.NewGuid(), MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "alice", Ct));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task MediaFlagRepository_UpdateStatusAsync_DismissWithoutReason_ThrowsAndChangesNothing(string? reason)
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, reason, "alice", Ct));

            Assert.Equal(MediaFlagStatus.Unreviewed, (await _repository.GetAsync(flag.Id, Ct))!.Status);
            Assert.Empty(await _actionLog.GetHistoryForEntityAsync("MediaFlag", flag.Id, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpdateStatusAsync_ToUnreviewed_ThrowsUseReopen()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);
            _ = await _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "x", "alice", Ct);

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Dismissed, MediaFlagStatus.Unreviewed, "x", "alice", Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpdateStatusAsync_EmptyOperator_Throws()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, " ", Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_ReopenAsync_Dismissed_ResetsAndAudits()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);
            _ = await _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "false alarm", "alice", Ct);

            MediaFlag reopened = await _repository.ReopenAsync(flag.Id, "admin", "dismissed in error", Ct);

            Assert.Equal(MediaFlagStatus.Unreviewed, reopened.Status);
            Assert.Null(reopened.ReviewedBy);
            Assert.Null(reopened.ReviewedAtUtc);
            Assert.Null(reopened.ReviewNote);
            IReadOnlyList<ActionLogEntry> log = await _actionLog.GetHistoryForEntityAsync("MediaFlag", flag.Id, Ct);
            Assert.Equal(2, log.Count);
            ActionLogEntry reopen = log.Single(e => e.Action == "ReopenMediaFlag");
            Assert.Equal("admin", reopen.Actor);
            Assert.Contains("dismissed in error", reopen.DetailsJson);
            Assert.Contains("Dismissed", reopen.DetailsJson);
            Assert.Contains("Unreviewed", reopen.DetailsJson);
        }

        [Fact]
        public async Task MediaFlagRepository_ReopenAsync_NotDismissed_ThrowsConflict()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);

            _ = await Assert.ThrowsAsync<FlagStatusConflictException>(() => _repository.ReopenAsync(flag.Id, "admin", "why", Ct));

            Assert.Empty(await _actionLog.GetHistoryForEntityAsync("MediaFlag", flag.Id, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_ReopenAsync_EmptyReason_Throws()
        {
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);
            _ = await _repository.UpdateStatusAsync(flag.Id, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "x", "alice", Ct);

            _ = await Assert.ThrowsAsync<ArgumentException>(() => _repository.ReopenAsync(flag.Id, "admin", "", Ct));
        }

        /// <summary>Stores a clip whose download event carries the given event type, recorded at the given instant.</summary>
        private async Task<MediaItem> AddClipWithEventTypeAsync(Guid deviceId, string eventType, DateTime recordedAtUtc)
        {
            DownloadEvent download = TestDataBuilder.BuildDownloadEvent(deviceId: deviceId);
            download.EventType = eventType;
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(Ct))
            {
                _ = db.DownloadEvents.Add(download);
                _ = await db.SaveChangesAsync(Ct);
            }

            MediaItem item = TestDataBuilder.BuildMediaItem(deviceId: deviceId, downloadEventId: download.Id);
            item.RecordedAtUtc = recordedAtUtc;
            await _media.AddAsync(item, Ct);
            return item;
        }

        [Fact]
        public async Task MediaFlagRepository_GetAsync_FlagWithClip_ReturnsClipEventType()
        {
            Guid deviceId = Guid.NewGuid();
            MediaItem clip = await AddClipWithEventTypeAsync(deviceId, "person", T0);
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(deviceId, clip.Id, MediaFlagType.BitrateCollapse), Ct);

            MediaFlag? loaded = await _repository.GetAsync(flag.Id, Ct);

            Assert.Equal("person", loaded!.EventType);
        }

        [Fact]
        public async Task MediaFlagRepository_GetAsync_RssiFlagWithOverlappingClip_ReturnsOverlappingEventType()
        {
            Guid deviceId = Guid.NewGuid();
            _ = await AddClipWithEventTypeAsync(deviceId, "vehicle", T0.AddMinutes(30));
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(deviceId, null, MediaFlagType.RssiDeviation), Ct);

            MediaFlag? loaded = await _repository.GetAsync(flag.Id, Ct);

            Assert.Equal("vehicle", loaded!.EventType);
        }

        [Fact]
        public async Task MediaFlagRepository_GetAsync_RssiFlagWithNoOverlappingClip_ReturnsNullEventType()
        {
            Guid deviceId = Guid.NewGuid();
            _ = await AddClipWithEventTypeAsync(deviceId, "person", T0.AddHours(3));
            MediaFlag flag = await _repository.UpsertRaisedAsync(Raised(deviceId, null, MediaFlagType.RssiDeviation), Ct);

            MediaFlag? loaded = await _repository.GetAsync(flag.Id, Ct);

            Assert.Null(loaded!.EventType);
        }

        [Fact]
        public async Task MediaFlagRepository_ListPageAsync_RssiFlagWithOverlappingClip_ReturnsOverlappingEventType()
        {
            Guid deviceId = Guid.NewGuid();
            _ = await AddClipWithEventTypeAsync(deviceId, "vehicle", T0.AddMinutes(30));
            _ = await _repository.UpsertRaisedAsync(Raised(deviceId, null, MediaFlagType.RssiDeviation), Ct);

            MediaFlagPage page = await _repository.ListPageAsync(new MediaFlagFilter(), 0, 10, Ct);

            Assert.Equal("vehicle", Assert.Single(page.Items).EventType);
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_DeviationSigma_RoundTrips()
        {
            MediaFlag raised = Raised(Guid.NewGuid(), null, MediaFlagType.RssiDeviation);
            raised.DeviationSigma = 2.4;
            MediaFlag stored = await _repository.UpsertRaisedAsync(raised, Ct);

            MediaFlag? loaded = await _repository.GetAsync(stored.Id, Ct);

            Assert.Equal(2.4, loaded!.DeviationSigma);
        }

        [Fact]
        public async Task MediaFlagRepository_SetDeviationSigmaAsync_FillsNullOnly()
        {
            IMediaFlagDeviationRepository deviation = _repository;
            MediaFlag flag = await RaiseAsync(Guid.NewGuid(), 0);
            Assert.Single(await deviation.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, Ct));

            await deviation.SetDeviationSigmaAsync(flag.Id, 2.2, Ct);
            await deviation.SetDeviationSigmaAsync(flag.Id, 9.9, Ct);

            Assert.Equal(2.2, (await _repository.GetAsync(flag.Id, Ct))!.DeviationSigma);
            Assert.Empty(await deviation.ListWithoutDeviationSigmaAsync(MediaFlagType.RssiDeviation, Ct));
        }

        [Fact]
        public async Task MediaFlagRepository_UpsertRaisedAsync_UnreviewedRow_RefreshesDeviationSigma()
        {
            MediaFlag first = Raised(Guid.NewGuid(), null, MediaFlagType.RssiDeviation);
            first.DeviationSigma = 2.1;
            _ = await _repository.UpsertRaisedAsync(first, Ct);

            MediaFlag second = Raised(first.DeviceId, null, MediaFlagType.RssiDeviation);
            second.DeviationSigma = 3.7;
            MediaFlag refreshed = await _repository.UpsertRaisedAsync(second, Ct);

            Assert.Equal(3.7, refreshed.DeviationSigma);
            Assert.Equal(3.7, (await _repository.GetAsync(refreshed.Id, Ct))!.DeviationSigma);
        }
    }
}
