using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Transport-agnostic sink for live-view telemetry and session state. The orchestrator calls this after
    /// persisting a change so that a real-time transport (e.g. SignalR) can push it to connected clients,
    /// without the orchestrator depending on any transport types.
    /// Publish failures are isolated by the caller: an implementation may throw, and the session lifecycle continues.
    /// </summary>
    public interface ILiveViewTelemetryPublisher
    {
        /// <summary>
        /// Publishes a visible state change of a live-view session (start, promote, demote, stop).
        /// </summary>
        /// <param name="session">The session in its newly persisted state.</param>
        /// <param name="ct">Cancellation token.</param>
        Task PublishSessionChangedAsync(LiveViewSession session, CancellationToken ct);
    }
}
