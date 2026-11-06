using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests that a logged <c>ChainRepaired</c> entry bridges a gap left by a removed entry, and that an altered entry
    /// is never accepted, even with a bridge present.
    /// </summary>
    public class ActionLogChainRepairTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ActionLogRepository _actionLog = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _actionLog = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task VerifyChainIntegrityAsync_RemovedEntryWithoutRepair_Fails()
        {
            // Arrange
            (_, _, ActionLogEntry last) = await AppendThreeAndRemoveMiddleAsync();

            // Act
            ChainIntegrityResult result = await _actionLog.VerifyChainIntegrityAsync(CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ChainBreakKind.PreviousHashMismatch, result.Break);
            Assert.Equal(last.Id, result.FailedEntryId);
        }

        [Fact]
        public async Task VerifyChainIntegrityAsync_GapBridgedByLoggedRepair_Passes()
        {
            // Arrange
            (ActionLogEntry first, ActionLogEntry removed, ActionLogEntry last) = await AppendThreeAndRemoveMiddleAsync();
            string bridge = ChainRepairRecord.Serialize([new ChainRepairRecord.Bridge(first.EntryHash, last.PreviousEntryHash!)]);

            // Act
            _ = await _actionLog.AppendAsync("test", ActorType.Human, ChainRepairRecord.Action, nameof(ActionLogEntry), null, bridge, CancellationToken.None);
            ChainIntegrityResult result = await _actionLog.VerifyChainIntegrityAsync(CancellationToken.None);

            // Assert
            Assert.True(result.IsValid, $"Chain should verify after a logged repair; removed entry was {removed.Id}.");
        }

        [Fact]
        public async Task VerifyChainIntegrityAsync_AlteredEntryWithBridge_StillFails()
        {
            // Arrange
            (ActionLogEntry first, ActionLogEntry removed, ActionLogEntry last) = await AppendThreeAndRemoveMiddleAsync();
            await using (VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                ActionLogEntry stored = await db.ActionLogEntries.SingleAsync(e => e.Id == last.Id);
                stored.Actor = "someone-else";
                _ = await db.SaveChangesAsync();
            }

            string bridge = ChainRepairRecord.Serialize([new ChainRepairRecord.Bridge(first.EntryHash, last.PreviousEntryHash!)]);
            _ = await _actionLog.AppendAsync("test", ActorType.Human, ChainRepairRecord.Action, nameof(ActionLogEntry), null, bridge, CancellationToken.None);

            // Act
            ChainIntegrityResult result = await _actionLog.VerifyChainIntegrityAsync(CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(ChainBreakKind.EntryHashMismatch, result.Break);
            Assert.Equal(last.Id, result.FailedEntryId);
        }

        // Appends three entries, then removes the middle one directly from the table, as an out-of-band deletion would.
        private async Task<(ActionLogEntry First, ActionLogEntry Removed, ActionLogEntry Last)> AppendThreeAndRemoveMiddleAsync()
        {
            ActionLogEntry first = await _actionLog.AppendAsync("test", ActorType.Human, "A", "MediaItem", Guid.NewGuid(), null, CancellationToken.None);
            ActionLogEntry removed = await _actionLog.AppendAsync("test", ActorType.Human, "B", "MediaItem", Guid.NewGuid(), null, CancellationToken.None);
            ActionLogEntry last = await _actionLog.AppendAsync("test", ActorType.Human, "C", "MediaItem", Guid.NewGuid(), null, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            ActionLogEntry toRemove = await db.ActionLogEntries.SingleAsync(e => e.Id == removed.Id);
            _ = db.ActionLogEntries.Remove(toRemove);
            _ = await db.SaveChangesAsync();

            return (first, removed, last);
        }
    }
}
