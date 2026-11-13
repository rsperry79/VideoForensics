using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for MediaFlag review state, with chain-of-custody logging on every review.</summary>
    public class MediaFlagRepository : IMediaFlagRepository, IMediaFlagDeviationRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MediaFlagRepository> _logger;

        /// <summary>Initializes a new instance of the MediaFlagRepository.</summary>
        public MediaFlagRepository(IUnitOfWork unitOfWork, ILogger<MediaFlagRepository> logger)
        {
            _unitOfWork = unitOfWork;
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
                try
                {
                    return await _unitOfWork.ExecuteAsync(async context =>
                    {
                        VideoForensicsDbContext db = context.Db();
                        MediaFlag staged = await MediaFlagOperations.StageUpsertAsync(db, raised, ct);
                        _ = await db.SaveChangesAsync(ct);
                        return staged;
                    }, ct);
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
            try
            {
                int removed = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    int count = await MediaFlagOperations.ReconcileAsync(db, mediaItemId, raised, ct);
                    _ = await db.SaveChangesAsync(ct);
                    return count;
                }, ct);

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
        public Task<MediaFlag?> GetAsync(Guid flagId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                return await MediaFlagOperations.FindWithEventTypeAsync(db, db.MediaFlags.AsNoTracking().Where(f => f.Id == flagId), ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<MediaFlagPage> ListPageAsync(MediaFlagFilter filter, int skip, int take, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context =>
            {
                return await MediaFlagOperations.ListPageAsync(context.Db(), filter, skip, take, ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<int> CountAsync(MediaFlagFilter filter, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context =>
            {
                return await MediaFlagOperations.CountAsync(context.Db(), filter, ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<MediaFlag?> GetNeighbourAsync(Guid flagId, MediaFlagFilter filter, MediaFlagNeighbour direction, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context =>
            {
                return await MediaFlagOperations.GetNeighbourAsync(context.Db(), flagId, filter, direction, ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<int?> GetPositionAsync(Guid flagId, MediaFlagFilter filter, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context =>
            {
                return await MediaFlagOperations.GetPositionAsync(context.Db(), flagId, filter, ct);
            }, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<MediaFlag>> ListWithoutDeviationSigmaAsync(MediaFlagType flagType, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<IReadOnlyList<MediaFlag>>(async context =>
            {
                return await context.Db().MediaFlags.AsNoTracking()
                    .Where(f => f.FlagType == flagType && f.DeviationSigma == null)
                    .OrderBy(f => f.Id)
                    .ToListAsync(ct);
            }, ct);
        }

        /// <inheritdoc />
        public async Task SetDeviationSigmaAsync(Guid flagId, double deviationSigma, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                // Only fills a null: a value stored by the raise path, or by an earlier pass, is never overwritten here.
                _ = await db.MediaFlags
                    .Where(f => f.Id == flagId && f.DeviationSigma == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.DeviationSigma, deviationSigma), ct);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
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
        /// Runs one review in a unit of work, so the status change and its audit entry commit together or not at all.
        /// The unit of work holds the chain gate from the chain-tail read through commit.
        /// </summary>
        private async Task<MediaFlag> ReviewAsync(Guid flagId, MediaFlagStatus expected, MediaFlagStatus target, string? reason, string operatorName, string action, CancellationToken ct)
        {
            try
            {
                MediaFlag result;
                try
                {
                    result = await _unitOfWork.ExecuteAsync(async context =>
                    {
                        VideoForensicsDbContext db = context.Db();
                        var actionLog = new UnitOfWorkActionLogRepository(db, _logger);
                        MediaFlag staged = await MediaFlagOperations.StageReviewAsync(db, actionLog, flagId, expected, target, reason, operatorName, action, ct);
                        _ = await db.SaveChangesAsync(ct);
                        return staged;
                    }, ct);
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
        }
    }
}
