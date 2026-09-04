using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for IntegrityRecord entities.</summary>
    public class IntegrityRecordRepository : IIntegrityRecordRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<IntegrityRecordRepository> _logger;

        /// <summary>Initializes a new instance of the IntegrityRecordRepository.</summary>
        public IntegrityRecordRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<IntegrityRecordRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Adds a new integrity record.</summary>
        public async Task AddAsync(IntegrityRecord record, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.IntegrityRecords.Add(record);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Integrity record added for media item {MediaItemId}: passed={Passed}", record.MediaItemId, record.Passed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding integrity record for media item {MediaItemId}", record.MediaItemId);
                throw;
            }
        }

        /// <summary>Gets the most recent integrity record for each of the given media items.</summary>
        public async Task<IReadOnlyList<IntegrityRecord>> GetLatestByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var ids = mediaItemIds.ToList();

            return await db.IntegrityRecords
                .Where(r => ids.Contains(r.MediaItemId))
                .GroupBy(r => r.MediaItemId)
                .Select(g => g.OrderByDescending(r => r.VerifiedAtUtc).First())
                .ToListAsync(ct);
        }
    }
}
