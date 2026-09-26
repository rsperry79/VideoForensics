namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a security event (login attempt, breach, lockout, etc.).
    /// </summary>
    /// <param name="Id">Unique identifier for the event.</param>
    /// <param name="OperatorId">The operator whose account this event relates to.</param>
    /// <param name="EventType">Type of security event (e.g. LoginAttemptFailure, LoginAttemptSuccess, BreachDetected).</param>
    /// <param name="Success">Whether the event outcome was successful or failed.</param>
    /// <param name="IpAddress">Client IP address where the event originated, if available.</param>
    /// <param name="OccurredAtUtc">Timestamp of when the event occurred, in UTC.</param>
    /// <param name="Reason">Additional context or reason for the event, if applicable.</param>
    public record SecurityEventDto(
        Guid Id,
        Guid OperatorId,
        string EventType,
        bool Success,
        string? IpAddress,
        DateTime OccurredAtUtc,
        string? Reason
    );

    /// <summary>
    /// Request DTO for querying security events.
    /// </summary>
    /// <param name="Limit">Maximum number of events to return (defaults to 20, max 1000).</param>
    /// <param name="Offset">Number of events to skip (for pagination).</param>
    /// <param name="OperatorId">Optional: specific operator ID to query. If omitted, returns the caller's own events. Cross-account queries require SuperAdmin + Local tier.</param>
    public record SecurityEventsQueryRequest(
        int? Limit = null,
        int? Offset = null,
        string? OperatorId = null
    );
}
