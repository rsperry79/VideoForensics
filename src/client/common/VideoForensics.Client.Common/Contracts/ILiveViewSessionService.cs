using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Orchestrates the lifecycle of live-view sessions for a device: starting, extending, promoting to
    /// sustained mode, stopping, and managing telemetry collection with interference scoring.
    /// </summary>
    public interface ILiveViewSessionService
    {
        /// <summary>
        /// Starts a live-view session for the given device. If an active session already exists for this
        /// device, returns it as-is (idempotent). Otherwise creates a new session, establishes the provider
        /// connection, subscribes to telemetry, and persists the initial row.
        /// </summary>
        /// <param name="deviceId">The device to start live view for.</param>
        /// <param name="reason">The trigger reason (Manual, JammingSuspected, JammingConfirmed, Calibration).</param>
        /// <param name="operatorId">The operator who triggered this, or null if system-triggered.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The persisted LiveViewSession, in Starting or Active state.</returns>
        /// <exception cref="InvalidOperationException">If live view is disabled in configuration.</exception>
        /// <exception cref="NotSupportedException">If the device's provider has no live-view capability.</exception>
        Task<LiveViewSession> StartAsync(Guid deviceId, LiveViewTriggerReason reason, Guid? operatorId, CancellationToken ct);

        /// <summary>
        /// Extends the idle timeout of an active session by updating its LastExtendedAtUtc timestamp.
        /// </summary>
        /// <param name="sessionId">The session to extend.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="KeyNotFoundException">If the session does not exist.</exception>
        Task ExtendAsync(Guid sessionId, CancellationToken ct);

        /// <summary>
        /// Promotes a session to sustained mode (idempotent). Sets IsSustained=true, SustainedSinceUtc=UtcNow,
        /// and State=Sustained. Dispatches a notification if a dispatcher is available.
        /// </summary>
        /// <param name="sessionId">The session to promote.</param>
        /// <param name="reason">Human-readable reason for promotion (e.g., "Sustained bitrate degradation").</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The promoted session.</returns>
        /// <exception cref="KeyNotFoundException">If the session does not exist.</exception>
        Task<LiveViewSession> PromoteToSustainedAsync(Guid sessionId, string reason, CancellationToken ct);

        /// <summary>
        /// Demotes a session from sustained mode back to active. Sets IsSustained=false, SustainedSinceUtc=null,
        /// and State=Active.
        /// </summary>
        /// <param name="sessionId">The session to demote.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The demoted session.</returns>
        /// <exception cref="KeyNotFoundException">If the session does not exist.</exception>
        Task<LiveViewSession> DemoteFromSustainedAsync(Guid sessionId, CancellationToken ct);

        /// <summary>
        /// Stops an active session: closes the underlying provider connection, stops all telemetry collection,
        /// and persists the final state. Idempotent if already stopped.
        /// </summary>
        /// <param name="sessionId">The session to stop.</param>
        /// <param name="stopReason">Reason for stopping (e.g., "IdleTimeout", "ManualStop", "SafetyValve").</param>
        /// <param name="ct">Cancellation token.</param>
        Task StopAsync(Guid sessionId, string stopReason, CancellationToken ct);

        /// <summary>
        /// Gets the currently active live-view session for a device, or null if none exists.
        /// </summary>
        /// <param name="deviceId">The device to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The active session, or null.</returns>
        Task<LiveViewSession?> GetActiveSessionAsync(Guid deviceId, CancellationToken ct);
    }
}
