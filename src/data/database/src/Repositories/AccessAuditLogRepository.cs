using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for evidence access audit trail tracking.</summary>
    public class AccessAuditLogRepository : IAccessAuditLogRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<AccessAuditLogRepository> _logger;

        public AccessAuditLogRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<AccessAuditLogRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<AccessAuditLogEntity> RecordAccessAsync(
            Guid evidenceId,
            string userId,
            string action,
            string ipAddress,
            string purpose,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var entry = new AccessAuditLogEntity
                {
                    Id = Guid.NewGuid(),
                    EvidenceId = evidenceId,
                    UserId = userId,
                    Action = action,
                    IpAddress = ipAddress,
                    Purpose = purpose,
                    AccessedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _ = db.AccessAuditLogs.Add(entry);
                _ = await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Recorded evidence access: EvidenceId={EvidenceId}, UserId={UserId}, Action={Action}, IpAddress={IpAddress}",
                    evidenceId, userId, action, ipAddress);

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording access audit log for evidence {EvidenceId}", evidenceId);
                throw;
            }
        }

        public async Task<AccessAuditLogEntity?> GetAsync(Guid logId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.AccessAuditLogs.FirstOrDefaultAsync(l => l.Id == logId, ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetForEvidenceAsync(Guid evidenceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.AccessAuditLogs
                .Where(l => l.EvidenceId == evidenceId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.AccessAuditLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.AccessAuditLogs
                .Where(l => l.AccessedAtUtc >= fromUtc && l.AccessedAtUtc <= toUtc)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.AccessAuditLogs
                .OrderByDescending(l => l.AccessedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }
    }
}
