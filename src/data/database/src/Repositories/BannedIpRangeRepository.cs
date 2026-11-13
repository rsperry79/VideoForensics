using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for BannedIpRange entities (manual IP address range bans for login prevention).</summary>
    public class BannedIpRangeRepository : IBannedIpRangeRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BannedIpRangeRepository> _logger;

        public BannedIpRangeRepository(IUnitOfWork unitOfWork, ILogger<BannedIpRangeRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IReadOnlyList<BannedIpRange>> GetActiveAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<BannedIpRange>>(async context =>
            {
                List<BannedIpRange> activeBans = await context.Db().BannedIpRanges
                    .Where(b => b.ExpiresAtUtc == null || b.ExpiresAtUtc > DateTime.UtcNow)
                    .ToListAsync(ct);

                return activeBans.AsReadOnly();
            }, ct);
        }

        public async Task AddAsync(BannedIpRange range, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.BannedIpRanges.Add(range);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
            _logger.LogDebug("IP range banned (expires: {ExpiresAtUtc})", range.ExpiresAtUtc);
        }

        public async Task RemoveAsync(Guid id, CancellationToken ct)
        {
            bool removed = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                BannedIpRange? ban = await db.BannedIpRanges.FirstOrDefaultAsync(b => b.Id == id, ct);
                if (ban == null)
                {
                    return false;
                }

                _ = db.BannedIpRanges.Remove(ban);
                _ = await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            if (removed)
            {
                _logger.LogDebug("IP range ban removed");
            }
        }
    }
}
