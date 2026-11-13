using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for NoticeDismissal entities (per-operator notice suppression).</summary>
    public class NoticeDismissalRepository : INoticeDismissalRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NoticeDismissalRepository> _logger;

        /// <summary>Initializes a new instance of the NoticeDismissalRepository.</summary>
        public NoticeDismissalRepository(IUnitOfWork unitOfWork, ILogger<NoticeDismissalRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Records an operator's dismissal of a notice (idempotent - multiple calls with the same (noticeId, operatorId) are safe).</summary>
        public async Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    NoticeDismissal? existing = await db.NoticeDismissals
                        .FirstOrDefaultAsync(nd => nd.NoticeId == noticeId && nd.OperatorId == operatorId, ct);

                    if (existing == null)
                    {
                        var dismissal = new NoticeDismissal
                        {
                            Id = Guid.NewGuid(),
                            NoticeId = noticeId,
                            OperatorId = operatorId,
                            DismissedUtc = DateTime.UtcNow
                        };

                        _ = await db.NoticeDismissals.AddAsync(dismissal, ct);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Notice dismissed: {NoticeId} by Operator {OperatorId}", noticeId, operatorId);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error dismissing notice: NoticeId {NoticeId}, OperatorId {OperatorId}: {Message} ({ErrorType})", noticeId, operatorId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Dismisses multiple notices for an operator in a single operation (idempotent).</summary>
        public async Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    var noticeIdsList = noticeIds.ToList();
                    if (noticeIdsList.Count == 0)
                    {
                        return null;
                    }

                    // Find existing dismissals
                    HashSet<Guid> existingDismissals = await db.NoticeDismissals
                        .Where(nd => noticeIdsList.Contains(nd.NoticeId) && nd.OperatorId == operatorId)
                        .Select(nd => nd.NoticeId)
                        .ToHashSetAsync(ct);

                    // Create dismissals only for notices not yet dismissed
                    var newDismissals = noticeIdsList
                        .Where(nid => !existingDismissals.Contains(nid))
                        .Select(nid => new NoticeDismissal
                        {
                            Id = Guid.NewGuid(),
                            NoticeId = nid,
                            OperatorId = operatorId,
                            DismissedUtc = DateTime.UtcNow
                        })
                        .ToList();

                    if (newDismissals.Count > 0)
                    {
                        await db.NoticeDismissals.AddRangeAsync(newDismissals, ct);
                        _ = await db.SaveChangesAsync(ct);
                        _logger.LogDebug("Dismissed {Count} notices for Operator {OperatorId}", newDismissals.Count, operatorId);
                    }

                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error dismissing multiple notices for Operator {OperatorId}: {Message} ({ErrorType})", operatorId, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
