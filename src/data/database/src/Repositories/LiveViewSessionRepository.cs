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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<LiveViewSessionRepository> _logger;

        /// <summary>Initializes a new instance of the LiveViewSessionRepository.</summary>
        public LiveViewSessionRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<LiveViewSessionRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Upserts (inserts or updates) a live-view session record.</summary>
        public async Task<LiveViewSession> UpsertSessionAsync(LiveViewSession session, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
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
                    _logger.LogInformation("Live-view session inserted: {SessionId}", session.Id);
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
                    _logger.LogInformation("Live-view session upserted (updated): {SessionId}", session.Id);
                }

                _ = await db.SaveChangesAsync(ct);
                return existing ?? session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting live-view session: {SessionId}", session.Id);
                throw;
            }
        }

        /// <summary>Gets a live-view session by ID.</summary>
        public async Task<LiveViewSession?> GetByIdAsync(Guid sessionId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LiveViewSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        }

        /// <summary>Gets the active live-view session for a device (State in Starting/Active/Sustained).</summary>
        public async Task<LiveViewSession?> GetActiveForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LiveViewSessions
                .Where(s => s.DeviceId == deviceId
                    && (s.State == LiveViewSessionState.Starting
                        || s.State == LiveViewSessionState.Active
                        || s.State == LiveViewSessionState.Sustained))
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Lists all active live-view sessions (State in Starting/Active/Sustained).</summary>
        public async Task<IReadOnlyList<LiveViewSession>> ListActiveAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LiveViewSessions
                .Where(s => s.State == LiveViewSessionState.Starting
                    || s.State == LiveViewSessionState.Active
                    || s.State == LiveViewSessionState.Sustained)
                .ToListAsync(ct);
        }

        /// <summary>Lists live-view sessions, optionally filtered by device and date range.</summary>
        public async Task<IReadOnlyList<LiveViewSession>> ListAsync(
            Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<LiveViewSession> query = db.LiveViewSessions.AsQueryable();

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
        }
    }
}
