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

        /// <summary>
        /// Publishes one persisted live-view telemetry sample (RTCP receiver report plus merged bitrate and
        /// interference score). Called by the orchestrator's per-session sampler after the row is written.
        /// The default implementation is a no-op so that existing publishers that only handle session state
        /// (such as the SignalR broadcaster) keep compiling until they opt in to sample delivery.
        /// </summary>
        /// <param name="sample">The telemetry sample in its persisted state.</param>
        /// <param name="ct">Cancellation token.</param>
        Task PublishSampleAsync(LiveViewTelemetrySample sample, CancellationToken ct) => Task.CompletedTask;
    }
}
