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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<BannedIpRangeRepository> _logger;

        public BannedIpRangeRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<BannedIpRangeRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<BannedIpRange>> GetActiveAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            List<BannedIpRange> activeBans = await db.BannedIpRanges
                .Where(b => b.ExpiresAtUtc == null || b.ExpiresAtUtc > DateTime.UtcNow)
                .ToListAsync(ct);

            return activeBans.AsReadOnly();
        }

        public async Task AddAsync(BannedIpRange range, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.BannedIpRanges.Add(range);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("IP range banned: {CidrRange} (expires: {ExpiresAtUtc})", range.CidrRange, range.ExpiresAtUtc);
        }

        public async Task RemoveAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            BannedIpRange? ban = await db.BannedIpRanges.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (ban == null)
            {
                return;
            }

            _ = db.BannedIpRanges.Remove(ban);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("IP range ban removed: {CidrRange}", ban.CidrRange);
        }
    }
}
