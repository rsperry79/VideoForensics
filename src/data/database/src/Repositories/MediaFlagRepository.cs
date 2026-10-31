using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for MediaFlag review state, with chain-of-custody logging on every review.</summary>
    public class MediaFlagRepository : IMediaFlagRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<MediaFlagRepository> _logger;

        /// <summary>Initializes a new instance of the MediaFlagRepository.</summary>
        public MediaFlagRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<MediaFlagRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<MediaFlag> UpsertRaisedAsync(MediaFlag raised, CancellationToken ct)
        {
            MediaFlagOperations.ValidateRaised(raised);

            // A second writer can insert the same key between our lookup and save; the unique index rejects one of
            // them, and the retry then finds the winner's row.
            for (int attempt = 1; ; attempt++)
            {
                await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
                try
                {
                    MediaFlag result = await MediaFlagOperations.StageUpsertAsync(db, raised, ct);
                    _ = await db.SaveChangesAsync(ct);
                    return result;
                }
                catch (DbUpdateException ex) when (attempt == 1)
                {
                    _logger.LogWarning("Media flag upsert for device {DeviceId} type {FlagType} hit a duplicate key ({ErrorType}); retrying", raised.DeviceId, raised.FlagType, ex.GetType().Name);
                    raised.Id = Guid.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error upserting media flag for device {DeviceId} type {FlagType}: {Message} ({ErrorType})", raised.DeviceId, raised.FlagType, ex.Message, ex.GetType().Name);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> ReconcileForMediaAsync(Guid mediaItemId, IReadOnlySet<MediaFlagType> raised, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                int removed = await MediaFlagOperations.ReconcileAsync(db, mediaItemId, raised, ct);
                if (removed > 0)
                {
                    _logger.LogInformation("Removed {Count} stale unreviewed flags for media item {MediaItemId}", removed, mediaItemId);
                }

                return removed;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError("Error reconciling flags for media item {MediaItemId}: {Message} ({ErrorType})", mediaItemId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<MediaFlag?> GetAsync(Guid flagId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.MediaFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Id == flagId, ct);
        }

        /// <inheritdoc />
        public async Task<MediaFlagPage> ListPageAsync(MediaFlagFilter filter, int skip, int take, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await MediaFlagOperations.ListPageAsync(db, filter, skip, take, ct);
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(MediaFlagFilter filter, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await MediaFlagOperations.CountAsync(db, filter, ct);
        }

        /// <inheritdoc />
        public async Task<MediaFlag?> GetNeighbourAsync(Guid flagId, MediaFlagFilter filter, MediaFlagNeighbour direction, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await MediaFlagOperations.GetNeighbourAsync(db, flagId, filter, direction, ct);
        }

        /// <inheritdoc />
        public async Task<MediaFlag> UpdateStatusAsync(Guid flagId, MediaFlagStatus expectedStatus, MediaFlagStatus newStatus, string? reason, string operatorName, CancellationToken ct)
        {
            MediaFlagOperations.ValidateReview(expectedStatus, newStatus, reason, operatorName);
            return await ReviewAsync(flagId, expectedStatus, newStatus, reason, operatorName, MediaFlagOperations.ActionFor(newStatus), ct);
        }

        /// <inheritdoc />
        public async Task<MediaFlag> ReopenAsync(Guid flagId, string operatorName, string reason, CancellationToken ct)
        {
            MediaFlagOperations.ValidateReopen(operatorName, reason);
            return await ReviewAsync(flagId, MediaFlagStatus.Dismissed, MediaFlagStatus.Unreviewed, reason, operatorName, MediaFlagOperations.ActionFor(MediaFlagStatus.Unreviewed), ct);
        }

        /// <summary>
        /// Runs one review in a transaction that holds the shared chain gate from the chain-tail read through commit, so the
        /// status change and its audit entry commit together or not at all.
        /// </summary>
        private async Task<MediaFlag> ReviewAsync(Guid flagId, MediaFlagStatus expected, MediaFlagStatus target, string? reason, string operatorName, string action, CancellationToken ct)
        {
            bool entered = !ActionLogAppendGate.IsHeldByCurrentFlow;
            if (entered)
            {
                await ActionLogAppendGate.WaitAsync(ct);
                ActionLogAppendGate.IsHeldByCurrentFlow = true;
            }

            try
            {
                MediaFlag result;
                try
                {
                    await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
                    using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
                    var actionLog = new UnitOfWorkActionLogRepository(db, _logger);
                    result = await MediaFlagOperations.StageReviewAsync(db, actionLog, flagId, expected, target, reason, operatorName, action, ct);
                    _ = await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another writer changed the row after we read it. Report the winner's state.
                    MediaFlag? current = await GetAsync(flagId, ct);
                    throw current is null
                        ? new KeyNotFoundException($"Flag {flagId} does not exist.")
                        : new FlagStatusConflictException(flagId, expected, current.Status, current.ReviewedBy, current.ReviewedAtUtc);
                }

                _logger.LogInformation("Media flag {FlagId} {OldStatus} -> {NewStatus} by {Operator}", flagId, expected, target, operatorName);
                return result;
            }
            catch (Exception ex) when (ex is not (ArgumentException or KeyNotFoundException))
            {
                // A conflict is an expected outcome the caller reports, so it logs at Warning.
                if (ex is FlagStatusConflictException)
                {
                    _logger.LogWarning("Media flag {FlagId} review by {Operator} conflicted: {Message}", flagId, operatorName, ex.Message);
                }
                else
                {
                    _logger.LogError("Error reviewing media flag {FlagId} by {Operator}: {Message} ({ErrorType})", flagId, operatorName, ex.Message, ex.GetType().Name);
                }

                throw;
            }
            finally
            {
                if (entered)
                {
                    ActionLogAppendGate.IsHeldByCurrentFlow = false;
                    ActionLogAppendGate.Release();
                }
            }
        }
    }
}
