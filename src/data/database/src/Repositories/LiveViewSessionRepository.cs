using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for live-view session records.</summary>
    public class LiveViewSessionRepository : ILiveViewSessionRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LiveViewSessionRepository> _logger;

        /// <summary>Initializes a new instance of the LiveViewSessionRepository.</summary>
        public LiveViewSessionRepository(IUnitOfWork unitOfWork, ILogger<LiveViewSessionRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Upserts (inserts or updates) a live-view session record.</summary>
        public async Task<LiveViewSession> UpsertSessionAsync(LiveViewSession session, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    LiveViewSession? existing = null;
                    if (session.Id != Guid.Empty)
                    {
                        existing = await db.LiveViewSessions.FirstOrDefaultAsync(s => s.Id == session.Id, ct);
                    }

                    if (existing == null)
                    {
                        if (session.Id == Guid.Empty)
                        {
                            session.Id = Guid.NewGuid();
                        }

                        _ = db.LiveViewSessions.Add(session);
                        _logger.LogDebug("Live-view session inserted: {SessionId}", session.Id);
                    }
                    else
                    {
                        existing.DeviceId = session.DeviceId;
                        existing.TriggerReason = session.TriggerReason;
                        existing.State = session.State;
                        existing.StartedAtUtc = session.StartedAtUtc;
                        existing.EndedAtUtc = session.EndedAtUtc;
                        existing.LastExtendedAtUtc = session.LastExtendedAtUtc;
                        existing.IsSustained = session.IsSustained;
                        existing.SustainedSinceUtc = session.SustainedSinceUtc;
                        existing.PromotionReason = session.PromotionReason;
                        existing.OperatorId = session.OperatorId;
                        existing.StopReason = session.StopReason;
                        existing.ProviderSessionRef = session.ProviderSessionRef;
                        _ = db.LiveViewSessions.Update(existing);
                        _logger.LogDebug("Live-view session upserted (updated): {SessionId}", session.Id);
                    }

                    _ = await db.SaveChangesAsync(ct);
                    return existing ?? session;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error upserting live-view session: {SessionId}: {Message} ({ErrorType})", session.Id, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets a live-view session by ID.</summary>
        public Task<LiveViewSession?> GetByIdAsync(Guid sessionId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().LiveViewSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct), ct);
        }

        /// <summary>Gets the active live-view session for a device (State in Starting/Active/Sustained).</summary>
        public Task<LiveViewSession?> GetActiveForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().LiveViewSessions
                .Where(s => s.DeviceId == deviceId
                    && (s.State == LiveViewSessionState.Starting
                        || s.State == LiveViewSessionState.Active
                        || s.State == LiveViewSessionState.Sustained))
                .FirstOrDefaultAsync(ct), ct);
        }

        /// <summary>Lists all active live-view sessions (State in Starting/Active/Sustained).</summary>
        public async Task<IReadOnlyList<LiveViewSession>> ListActiveAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().LiveViewSessions
                .Where(s => s.State == LiveViewSessionState.Starting
                    || s.State == LiveViewSessionState.Active
                    || s.State == LiveViewSessionState.Sustained)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists live-view sessions, optionally filtered by device and date range.</summary>
        public async Task<IReadOnlyList<LiveViewSession>> ListAsync(
            Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                IQueryable<LiveViewSession> query = context.Db().LiveViewSessions.AsQueryable();

                if (deviceId.HasValue)
                {
                    query = query.Where(s => s.DeviceId == deviceId.Value);
                }

                if (fromUtc.HasValue)
                {
                    query = query.Where(s => s.StartedAtUtc >= fromUtc.Value);
                }

                if (toUtc.HasValue)
                {
                    query = query.Where(s => s.StartedAtUtc <= toUtc.Value);
                }

                return await query.ToListAsync(ct);
            }, ct);
        }
    }
}
