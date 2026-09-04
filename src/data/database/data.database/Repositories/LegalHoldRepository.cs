using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for LegalHold entities.</summary>
    public class LegalHoldRepository : ILegalHoldRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ILogger<LegalHoldRepository> _logger;

        /// <summary>Initializes a new instance of the LegalHoldRepository.</summary>
        public LegalHoldRepository(IDbContextFactory<VideoForensicsDbContext> factory, IActionLogRepository actionLogRepository, ILogger<LegalHoldRepository> logger)
        {
            _factory = factory;
            _actionLogRepository = actionLogRepository;
            _logger = logger;
        }

        /// <summary>Places a new legal hold on a media item, recording the reason and actor. Also appends a chain-of-custody ActionLog entry ("PlaceLegalHold") in the same operation.</summary>
        public async Task<LegalHold> PlaceAsync(Guid mediaItemId, string reason, string createdBy, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var hold = new LegalHold
                {
                    Id = Guid.NewGuid(),
                    MediaItemId = mediaItemId,
                    Reason = reason,
                    CreatedBy = createdBy,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.LegalHolds.Add(hold);
                await db.SaveChangesAsync(ct);

                await _actionLogRepository.AppendAsync(
                    createdBy,
                    ActorType.Human,
                    "PlaceLegalHold",
                    "MediaItem",
                    mediaItemId,
                    $"Legal hold reason: {reason}",
                    ct);

                _logger.LogInformation("Legal hold placed on media item {MediaItemId} by {CreatedBy}", mediaItemId, createdBy);

                return hold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing legal hold on media item {MediaItemId}", mediaItemId);
                throw;
            }
        }

        /// <summary>Releases an active legal hold, recording who released it and why. Also appends a chain-of-custody ActionLog entry ("ReleaseLegalHold") in the same operation. Throws InvalidOperationException if the hold does not exist or is already released.</summary>
        public async Task ReleaseAsync(Guid legalHoldId, string releasedBy, string releaseReason, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var hold = await db.LegalHolds.FirstOrDefaultAsync(h => h.Id == legalHoldId, ct);
                if (hold is null || hold.ReleasedAtUtc is not null)
                {
                    throw new InvalidOperationException($"Legal hold {legalHoldId} does not exist or is already released.");
                }

                hold.ReleasedBy = releasedBy;
                hold.ReleasedAtUtc = DateTime.UtcNow;
                hold.ReleaseReason = releaseReason;

                await db.SaveChangesAsync(ct);

                await _actionLogRepository.AppendAsync(
                    releasedBy,
                    ActorType.Human,
                    "ReleaseLegalHold",
                    "MediaItem",
                    hold.MediaItemId,
                    $"Legal hold release reason: {releaseReason}",
                    ct);

                _logger.LogInformation("Legal hold {LegalHoldId} released on media item {MediaItemId} by {ReleasedBy}", legalHoldId, hold.MediaItemId, releasedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing legal hold {LegalHoldId}", legalHoldId);
                throw;
            }
        }

        /// <summary>Gets the currently-active legal hold (ReleasedAtUtc == null) for each of the given media items that has one. Media items with no active hold are simply absent from the result.</summary>
        public async Task<IReadOnlyList<LegalHold>> GetActiveByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var ids = mediaItemIds.ToList();

            return await db.LegalHolds
                .Where(h => ids.Contains(h.MediaItemId) && h.ReleasedAtUtc == null)
                .ToListAsync(ct);
        }
    }
}
