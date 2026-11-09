using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for SecurityAuditLogEntry rows.</summary>
    public class SecurityAuditLogRepository : ISecurityAuditLogRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SecurityAuditLogRepository> _logger;

        public SecurityAuditLogRepository(IUnitOfWork unitOfWork, ILogger<SecurityAuditLogRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<SecurityAuditLogEntry> AppendAsync(SecurityAuditLogEntry entry, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.SecurityAuditLogEntries.Add(entry);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);

            if (entry.IsUrgent)
            {
                _logger.LogWarning("URGENT security event: {EventType} (Operator={OperatorId}, Device={PairedDeviceId})",
                    entry.EventType, entry.OperatorId, entry.PairedDeviceId);
            }
            else
            {
                _logger.LogDebug("Security event: {EventType}", entry.EventType);
            }

            return entry;
        }

        public async Task<IReadOnlyList<SecurityAuditLogEntry>> ListAsync(Guid? operatorId, int maxResults, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<SecurityAuditLogEntry>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                IQueryable<SecurityAuditLogEntry> query = db.SecurityAuditLogEntries.AsQueryable();
                if (operatorId.HasValue)
                {
                    query = query.Where(e => e.OperatorId == operatorId.Value);
                }

                return await query
                    .OrderByDescending(e => e.TimestampUtc)
                    .Take(maxResults)
                    .ToListAsync(ct);
            }, ct);
        }
    }
}
