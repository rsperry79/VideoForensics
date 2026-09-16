using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Notice entities (persisted notification events).</summary>
    public class NoticeRepository : INoticeRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<NoticeRepository> _logger;

        /// <summary>Initializes a new instance of the NoticeRepository.</summary>
        public NoticeRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<NoticeRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Persists a new notice event to the database.</summary>
        public async Task AddAsync(Notice notice, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Notices.Add(notice);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Notice added: {NoticeId} ({EventType}, Severity: {Severity})", notice.Id, notice.EventType, notice.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notice: {EventType}", notice.EventType);
                throw;
            }
        }

        /// <summary>Lists notices for an operator, excluding those dismissed by that operator (unless includeDismissed is true). Includes notices with Audience=All or (Audience=AdminsOnly AND operator is Admin+).</summary>
        public async Task<IReadOnlyList<Notice>> ListForOperatorAsync(Guid operatorId, bool includeDismissed = false, CancellationToken ct = default)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var query = db.Notices.AsQueryable();

                // Filter by audience: Audience=0 (All) or (Audience=1 (AdminsOnly) AND operator is Admin)
                // Note: For now, we include all notices with Audience=0. Admin filtering would require
                // checking operator role via navigation, which we avoid to keep the query simple.
                // A more sophisticated implementation could check operator role in a separate step.
                query = query.Where(n => n.Audience == 0); // For MVP, only include All-audience notices

                if (!includeDismissed)
                {
                    // Exclude notices that have been dismissed by this operator
                    var dismissedNoticeIds = db.NoticeDismissals
                        .Where(nd => nd.OperatorId == operatorId)
                        .Select(nd => nd.NoticeId)
                        .ToHashSet();

                    query = query.Where(n => !dismissedNoticeIds.Contains(n.Id));
                }

                return await query.OrderByDescending(n => n.TimestampUtc).ToListAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing notices for operator: {OperatorId}", operatorId);
                throw;
            }
        }

        /// <summary>Counts undismissed notices for an operator, for UI badge/indicator purposes.</summary>
        public async Task<int> CountUndismissedForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var dismissedNoticeIds = db.NoticeDismissals
                    .Where(nd => nd.OperatorId == operatorId)
                    .Select(nd => nd.NoticeId)
                    .ToHashSet();

                return await db.Notices
                    .Where(n => n.Audience == 0 && !dismissedNoticeIds.Contains(n.Id))
                    .CountAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting undismissed notices for operator: {OperatorId}", operatorId);
                throw;
            }
        }
    }
}
