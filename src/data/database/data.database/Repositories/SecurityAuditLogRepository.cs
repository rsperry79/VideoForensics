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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SecurityAuditLogRepository> _logger;

        public SecurityAuditLogRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<SecurityAuditLogRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<SecurityAuditLogEntry> AppendAsync(SecurityAuditLogEntry entry, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.SecurityAuditLogEntries.Add(entry);
            await db.SaveChangesAsync(ct);

            if (entry.IsUrgent)
            {
                _logger.LogWarning("URGENT security event: {EventType} (Operator={OperatorId}, Device={PairedDeviceId}, Ip={SourceIp})",
                    entry.EventType, entry.OperatorId, entry.PairedDeviceId, entry.SourceIp);
            }
            else
            {
                _logger.LogInformation("Security event: {EventType}", entry.EventType);
            }

            return entry;
        }

        public async Task<IReadOnlyList<SecurityAuditLogEntry>> ListAsync(Guid? operatorId, int maxResults, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var query = db.SecurityAuditLogEntries.AsQueryable();
            if (operatorId.HasValue)
            {
                query = query.Where(e => e.OperatorId == operatorId.Value);
            }

            return await query
                .OrderByDescending(e => e.TimestampUtc)
                .Take(maxResults)
                .ToListAsync(ct);
        }
    }
}
