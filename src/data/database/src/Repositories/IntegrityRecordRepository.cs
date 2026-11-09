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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<IntegrityRecordRepository> _logger;

        /// <summary>Initializes a new instance of the IntegrityRecordRepository.</summary>
        public IntegrityRecordRepository(IUnitOfWork unitOfWork, ILogger<IntegrityRecordRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Adds a new integrity record.</summary>
        public async Task AddAsync(IntegrityRecord record, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.IntegrityRecords.Add(record);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Integrity record added for media item {MediaItemId}: passed={Passed}", record.MediaItemId, record.Passed);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding integrity record for media item {MediaItemId}: {Message} ({ErrorType})", record.MediaItemId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets the most recent integrity record for each of the given media items.</summary>
        public async Task<IReadOnlyList<IntegrityRecord>> GetLatestByMediaItemIdsAsync(IEnumerable<Guid> mediaItemIds, CancellationToken ct)
        {
            var ids = mediaItemIds.ToList();

            return await _unitOfWork.ReadAsync(context => context.Db().IntegrityRecords
                .Where(r => ids.Contains(r.MediaItemId))
                .GroupBy(r => r.MediaItemId)
                .Select(g => g.OrderByDescending(r => r.VerifiedAtUtc).First())
                .ToListAsync(ct), ct);
        }
    }
}
