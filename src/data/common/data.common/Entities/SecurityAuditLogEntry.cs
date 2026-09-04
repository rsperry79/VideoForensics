namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A security-surface event (plan §5.5) - deliberately separate from the evidence
    /// chain-of-custody trail (<see cref="ActionLogEntry"/>): this is about server security
    /// (pairing, auth, network/tunnel changes, revocation, provider-API health), not evidence
    /// handling. Written by the pairing endpoints, the auth middleware (including every
    /// session-start verification, not just failures), network-tier changes, tunnel start/stop, and
    /// the provider API budget guard.
    /// </summary>
    public class SecurityAuditLogEntry
    {
        public Guid Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public required string EventType { get; set; }
        public Guid? OperatorId { get; set; }
        public Guid? PairedDeviceId { get; set; }
        public string? SourceIp { get; set; }
        public string? Details { get; set; }
        public bool IsUrgent { get; set; }
    }

    /// <summary>Well-known <see cref="SecurityAuditLogEntry.EventType"/> values (plan §5.5/§5.12) - a plain string column rather than an enum so a new event type never needs an EF migration.</summary>
    public static class SecurityAuditEventTypes
    {
        public const string PairingInitiated = nameof(PairingInitiated);
        public const string PairingCompleted = nameof(PairingCompleted);
        public const string PairingRevoked = nameof(PairingRevoked);
        public const string OperatorDeactivated = nameof(OperatorDeactivated);
        public const string AuthSuccess = nameof(AuthSuccess);
        public const string AuthFailure = nameof(AuthFailure);
        public const string SessionVerified = nameof(SessionVerified);
        public const string StepUpVerified = nameof(StepUpVerified);
        public const string StepUpFailed = nameof(StepUpFailed);
        public const string RateLimitLockout = nameof(RateLimitLockout);
        public const string NetworkTierChanged = nameof(NetworkTierChanged);
        public const string SuperAdminActionDeniedRemote = nameof(SuperAdminActionDeniedRemote);
        public const string CertificateFingerprintMismatch = nameof(CertificateFingerprintMismatch);
        public const string TunnelStarted = nameof(TunnelStarted);
        public const string TunnelStopped = nameof(TunnelStopped);
        public const string ProviderRateLimitHit = nameof(ProviderRateLimitHit);
        public const string ProviderApiVolumeAnomaly = nameof(ProviderApiVolumeAnomaly);

        /// <summary>
        /// The urgency each event type is logged with today at its actual call site, absent any
        /// operator override (plan §5.6's per-event-type notification toggle reads this as the
        /// baseline). Kept next to the constants it describes so the two never drift apart - if a
        /// call site's own <c>isUrgent:</c> argument changes, this table should change with it.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, bool> DefaultUrgency = new Dictionary<string, bool>
        {
            [PairingInitiated] = false,
            [PairingCompleted] = true,
            [PairingRevoked] = true,
            [OperatorDeactivated] = true,
            [AuthSuccess] = false,
            [AuthFailure] = true,
            [SessionVerified] = false,
            [StepUpVerified] = false,
            [StepUpFailed] = true,
            [RateLimitLockout] = true,
            [NetworkTierChanged] = true,
            [SuperAdminActionDeniedRemote] = true,
            [CertificateFingerprintMismatch] = true,
            [TunnelStarted] = true,
            [TunnelStopped] = true,
            [ProviderRateLimitHit] = true,
            [ProviderApiVolumeAnomaly] = true,
        };
    }
}
