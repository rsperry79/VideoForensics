using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for live-view session records and state management.</summary>
    public interface ILiveViewSessionRepository
    {
        /// <summary>Upserts (inserts or updates) a live-view session record.</summary>
        Task<LiveViewSession> UpsertSessionAsync(LiveViewSession session, CancellationToken ct);

        /// <summary>Gets a live-view session by ID.</summary>
        Task<LiveViewSession?> GetByIdAsync(Guid sessionId, CancellationToken ct);

        /// <summary>Gets the active live-view session for a device (State in Starting/Active/Sustained).</summary>
        Task<LiveViewSession?> GetActiveForDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all active live-view sessions (State in Starting/Active/Sustained).</summary>
        Task<IReadOnlyList<LiveViewSession>> ListActiveAsync(CancellationToken ct);

        /// <summary>Lists live-view sessions, optionally filtered by device and date range.</summary>
        Task<IReadOnlyList<LiveViewSession>> ListAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
    }
}
