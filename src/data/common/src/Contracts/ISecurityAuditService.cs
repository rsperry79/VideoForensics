namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Security audit log service for recording and querying operator security events.</summary>
    public interface ISecurityAuditService
    {
        /// <summary>Records a login attempt (success or failure) with optional IP and reason.</summary>
        Task RecordLoginAttemptAsync(Guid operatorId, string? ipAddress, bool success, string? reason, CancellationToken ct);

        /// <summary>Records a breach detection event for an operator.</summary>
        Task RecordBreachDetectionAsync(Guid operatorId, string? ipAddress, string reason, CancellationToken ct);

        /// <summary>Records an account lockout event.</summary>
        Task RecordAccountLockoutAsync(Guid operatorId, string? ipAddress, CancellationToken ct);

        /// <summary>Records an account lockout release (manual unlock by SuperAdmin).</summary>
        Task RecordAccountLockoutReleasedAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Gets security events for a specific operator, paged.</summary>
        IAsyncEnumerable<SecurityEventDto> GetOperatorEventsAsync(Guid operatorId, int skip, int take, CancellationToken ct);

        /// <summary>Gets all security events across all operators, paged (for SuperAdmin audit queries).</summary>
        IAsyncEnumerable<SecurityEventDto> GetAllEventsAsync(int skip, int take, CancellationToken ct);
    }

    /// <summary>Data transfer object for security event audit log entries.</summary>
    public class SecurityEventDto
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? IpAddress { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }
    }
}
