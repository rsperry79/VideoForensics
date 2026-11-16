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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AccessAuditLogRepository> _logger;

        public AccessAuditLogRepository(
            IUnitOfWork unitOfWork,
            ILogger<AccessAuditLogRepository> logger)
        {
            _unitOfWork = unitOfWork;
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
            try
            {
                AccessAuditLogEntity recorded = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
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
                    return entry;
                }, ct);

                _logger.LogDebug(
                    "Recorded evidence access: EvidenceId={EvidenceId}, Action={Action}",
                    evidenceId, action);

                return recorded;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording access audit log for evidence {EvidenceId}: {Message} ({ErrorType})", evidenceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<AccessAuditLogEntity?> GetAsync(Guid logId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().AccessAuditLogs.FirstOrDefaultAsync(l => l.Id == logId, ct), ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetForEvidenceAsync(Guid evidenceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().AccessAuditLogs
                .Where(l => l.EvidenceId == evidenceId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().AccessAuditLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().AccessAuditLogs
                .Where(l => l.AccessedAtUtc >= fromUtc && l.AccessedAtUtc <= toUtc)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<AccessAuditLogEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().AccessAuditLogs
                .OrderByDescending(l => l.AccessedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct), ct);
        }
    }
}
