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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NoticeRepository> _logger;

        /// <summary>Initializes a new instance of the NoticeRepository.</summary>
        public NoticeRepository(IUnitOfWork unitOfWork, ILogger<NoticeRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Persists a new notice event to the database.</summary>
        public async Task AddAsync(Notice notice, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.Notices.Add(notice);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Notice added: {NoticeId} ({EventType}, Severity: {Severity})", notice.Id, notice.EventType, notice.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding notice: {EventType}: {Message} ({ErrorType})", notice.EventType, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists notices for an operator, excluding those dismissed by that operator (unless includeDismissed is true). Includes notices with Audience=All, plus Audience=AdminsOnly when isAdmin is true.</summary>
        public async Task<IReadOnlyList<Notice>> ListForOperatorAsync(Guid operatorId, bool isAdmin, bool includeDismissed = false, CancellationToken ct = default)
        {
            try
            {
                return await _unitOfWork.ReadAsync<IReadOnlyList<Notice>>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    IQueryable<Notice> query = db.Notices.AsQueryable();

                    // Filter by audience: Audience=0 (All) is always included; Audience=1 (AdminsOnly) is included
                    // only when the caller has already determined the operator is Admin+ (avoids a DB join/role lookup
                    // here — the caller resolves role from session state).
                    query = query.Where(n => n.Audience == 0 || (n.Audience == 1 && isAdmin));

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
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error listing notices for operator: {OperatorId}: {Message} ({ErrorType})", operatorId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsWithDetailsAsync(string marker, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrEmpty(marker);

            return await _unitOfWork.ReadAsync<bool>(context =>
                context.Db().Notices.AnyAsync(n => n.Details != null && n.Details.Contains(marker), ct), ct);
        }

        /// <summary>Counts undismissed notices for an operator, for UI badge/indicator purposes. Includes Audience=AdminsOnly notices only when isAdmin is true.</summary>
        public async Task<int> CountUndismissedForOperatorAsync(Guid operatorId, bool isAdmin, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ReadAsync<int>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    var dismissedNoticeIds = db.NoticeDismissals
                        .Where(nd => nd.OperatorId == operatorId)
                        .Select(nd => nd.NoticeId)
                        .ToHashSet();

                    return await db.Notices
                        .Where(n => (n.Audience == 0 || (n.Audience == 1 && isAdmin)) && !dismissedNoticeIds.Contains(n.Id))
                        .CountAsync(ct);
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error counting undismissed notices for operator: {OperatorId}: {Message} ({ErrorType})", operatorId, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
