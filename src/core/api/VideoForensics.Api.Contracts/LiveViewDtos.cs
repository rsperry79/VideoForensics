using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Wire representation of a live-view session. Enums are sent as their names (e.g., "Manual", "Active").
    /// </summary>
    /// <param name="Id">Unique identifier for the session.</param>
    /// <param name="DeviceId">The device the session is streaming from.</param>
    /// <param name="TriggerReason">Name of the <see cref="LiveViewTriggerReason"/> that started the session.</param>
    /// <param name="State">Name of the <see cref="LiveViewSessionState"/> the session is currently in.</param>
    /// <param name="StartedAtUtc">Timestamp when the session started, in UTC.</param>
    /// <param name="EndedAtUtc">Timestamp when the session ended, in UTC. Null while the session is still open.</param>
    /// <param name="LastExtendedAtUtc">Timestamp of the most recent idle-timeout extension, in UTC.</param>
    /// <param name="IsSustained">True if the session has been promoted to sustained mode.</param>
    /// <param name="SustainedSinceUtc">Timestamp when sustained mode began, in UTC. Null if not sustained.</param>
    /// <param name="PromotionReason">Human-readable reason the session was promoted. Null if not promoted.</param>
    /// <param name="OperatorId">The operator who started the session. Null if system-triggered.</param>
    /// <param name="StopReason">Reason the session was stopped (e.g., "IdleTimeout"). Null while open.</param>
    /// <param name="ProviderSessionRef">Provider-specific reference for the underlying live connection. Null if unknown.</param>
    public record LiveViewSessionDto(
        Guid Id,
        Guid DeviceId,
        string TriggerReason,
        string State,
        DateTime StartedAtUtc,
        DateTime? EndedAtUtc,
        DateTime LastExtendedAtUtc,
        bool IsSustained,
        DateTime? SustainedSinceUtc,
        string? PromotionReason,
        Guid? OperatorId,
        string? StopReason,
        string? ProviderSessionRef
    );

    /// <summary>
    /// Request body for starting a live-view session. The operator is taken from the server's auth context,
    /// never from the client, so no operator identifier is sent.
    /// </summary>
    /// <param name="DeviceId">The device to start live view for.</param>
    /// <param name="Reason">Name of the <see cref="LiveViewTriggerReason"/> (e.g., "Manual").</param>
    public record StartLiveViewRequestDto(
        Guid DeviceId,
        string Reason
    );

    /// <summary>
    /// Request body for stopping a live-view session.
    /// </summary>
    /// <param name="StopReason">Reason for stopping (e.g., "ManualStop", "IdleTimeout").</param>
    public record StopLiveViewRequestDto(
        string StopReason
    );

    /// <summary>Extension methods for mapping live-view session entities to wire DTOs.</summary>
    public static class LiveViewDtoMapping
    {
        /// <summary>Converts a LiveViewSession entity to a LiveViewSessionDto, sending enums as their names.</summary>
        public static LiveViewSessionDto ToDto(this LiveViewSession session)
        {
            return new LiveViewSessionDto(
                Id: session.Id,
                DeviceId: session.DeviceId,
                TriggerReason: session.TriggerReason.ToString(),
                State: session.State.ToString(),
                StartedAtUtc: session.StartedAtUtc,
                EndedAtUtc: session.EndedAtUtc,
                LastExtendedAtUtc: session.LastExtendedAtUtc,
                IsSustained: session.IsSustained,
                SustainedSinceUtc: session.SustainedSinceUtc,
                PromotionReason: session.PromotionReason,
                OperatorId: session.OperatorId,
                StopReason: session.StopReason,
                ProviderSessionRef: session.ProviderSessionRef
            );
        }
    }
}
