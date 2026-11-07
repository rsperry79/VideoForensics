namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Security audit log service for recording and querying operator security events.</summary>
    public interface ISecurityAuditService
    {
        /// <summary>Records a login attempt (success or failure) with optional IP and reason.</summary>
        Task RecordLoginAttemptAsync(Guid operatorId, string? ipAddress, bool success, string? reason, CancellationToken ct);

        /// <summary>Records a breach detection event for an operator.</summary>
        Task RecordBreachDetectionAsync(Guid operatorId, string? ipAddress, string reason, CancellationToken ct);

        /// <summary>
        /// Records a failed login whose username matches no operator. The event has no operator; the typed username
        /// is stored trimmed and truncated to 256 characters so the unknown-login filter can show it. The password is never recorded.
        /// </summary>
        /// <param name="attemptedUsername">The username as typed; null or blank stores no username.</param>
        /// <param name="ipAddress">Client IP address the attempt came from, if available.</param>
        /// <param name="reason">Short reason for the failure.</param>
        /// <param name="ct">Cancellation token.</param>
        Task RecordUnknownUserLoginAsync(string? attemptedUsername, string? ipAddress, string? reason, CancellationToken ct);

        /// <summary>Records an account lockout event.</summary>
        Task RecordAccountLockoutAsync(Guid operatorId, string? ipAddress, CancellationToken ct);

        /// <summary>Records an account lockout release (manual unlock by SuperAdmin).</summary>
        Task RecordAccountLockoutReleasedAsync(Guid operatorId, CancellationToken ct);

        /// <summary>
        /// Gets security events paged. When <paramref name="unknownOnly"/> is false, returns the events of
        /// <paramref name="operatorId"/> (required). When true, returns only events with no operator (failed logins
        /// whose username matched no operator); <paramref name="operatorId"/> is ignored and should be null.
        /// </summary>
        IAsyncEnumerable<SecurityEventDto> GetOperatorEventsAsync(Guid? operatorId, bool unknownOnly, int skip, int take, CancellationToken ct);

        /// <summary>Gets all security events across all operators, paged (for SuperAdmin audit queries).</summary>
        IAsyncEnumerable<SecurityEventDto> GetAllEventsAsync(int skip, int take, CancellationToken ct);
    }

    /// <summary>Data transfer object for security event audit log entries.</summary>
    public class SecurityEventDto
    {
        public Guid Id { get; set; }
        /// <summary>The operator the event relates to; null for a failed login whose username matches no operator.</summary>
        public Guid? OperatorId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? IpAddress { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }

        /// <summary>The username typed by a failed login whose username matches no operator; null otherwise.</summary>
        public string? AttemptedUsername { get; set; }
    }
}
