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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<NoticeDismissalRepository> _logger;

        /// <summary>Initializes a new instance of the NoticeDismissalRepository.</summary>
        public NoticeDismissalRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<NoticeDismissalRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Records an operator's dismissal of a notice (idempotent - multiple calls with the same (noticeId, operatorId) are safe).</summary>
        public async Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
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
                    _logger.LogInformation("Notice dismissed: {NoticeId} by Operator {OperatorId}", noticeId, operatorId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing notice: NoticeId {NoticeId}, OperatorId {OperatorId}", noticeId, operatorId);
                throw;
            }
        }

        /// <summary>Dismisses multiple notices for an operator in a single operation (idempotent).</summary>
        public async Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var noticeIdsList = noticeIds.ToList();
                if (noticeIdsList.Count == 0)
                {
                    return;
                }

                // Find existing dismissals
                var existingDismissals = await db.NoticeDismissals
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
                    _logger.LogInformation("Dismissed {Count} notices for Operator {OperatorId}", newDismissals.Count, operatorId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing multiple notices for Operator {OperatorId}", operatorId);
                throw;
            }
        }
    }
}
