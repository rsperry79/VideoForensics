namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Security event audit log for login attempts, breaches, account lockouts, and IP bans.
    /// Write-once append-only log; no updates or deletes.
    /// </summary>
    public class SecurityEvent
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }
        public SecurityEventType EventType { get; set; }
        public bool Success { get; set; }
        public string? IpAddress { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>Security event types for operator authentication and account security.</summary>
    public enum SecurityEventType
    {
        LoginAttemptFailure = 0,
        LoginAttemptSuccess = 1,
        BreachDetected = 2,
        IpBanned = 3,
        IpUnbanned = 4,
        LockedOut = 5,
        LockedOutReleased = 6,
        TwoFactorRequired = 7,
        TwoFactorSuccess = 8,
        TwoFactorFailure = 9
    }
}
