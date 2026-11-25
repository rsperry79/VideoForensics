namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Security audit event type names for the client. Mirrors the data-layer SecurityAuditEventTypes constants.</summary>
    public static class SecurityAuditEventTypesDto
    {
        /// <summary>Audit event type: pairing initiated.</summary>
        public const string PairingInitiated = nameof(PairingInitiated);
        /// <summary>Audit event type: pairing completed.</summary>
        public const string PairingCompleted = nameof(PairingCompleted);
        /// <summary>Audit event type: pairing revoked.</summary>
        public const string PairingRevoked = nameof(PairingRevoked);
        /// <summary>Audit event type: pairing removed.</summary>
        public const string PairingRemoved = nameof(PairingRemoved);
        /// <summary>Audit event type: device code issued.</summary>
        public const string DeviceCodeIssued = nameof(DeviceCodeIssued);
        /// <summary>Audit event type: device code approved.</summary>
        public const string DeviceCodeApproved = nameof(DeviceCodeApproved);
        /// <summary>Audit event type: device code expired.</summary>
        public const string DeviceCodeExpired = nameof(DeviceCodeExpired);
        /// <summary>Audit event type: operator deactivated.</summary>
        public const string OperatorDeactivated = nameof(OperatorDeactivated);
        /// <summary>Audit event type: operator approved.</summary>
        public const string OperatorApproved = nameof(OperatorApproved);
        /// <summary>Audit event type: operator registered.</summary>
        public const string OperatorRegistered = nameof(OperatorRegistered);
        /// <summary>Audit event type: password reset.</summary>
        public const string PasswordReset = nameof(PasswordReset);
        /// <summary>Audit event type: credential registered.</summary>
        public const string CredentialRegistered = nameof(CredentialRegistered);
        /// <summary>Audit event type: credential approved.</summary>
        public const string CredentialApproved = nameof(CredentialApproved);
        /// <summary>Audit event type: credential revoked.</summary>
        public const string CredentialRevoked = nameof(CredentialRevoked);
        /// <summary>Audit event type: authentication succeeded.</summary>
        public const string AuthSuccess = nameof(AuthSuccess);
        /// <summary>Audit event type: authentication failed.</summary>
        public const string AuthFailure = nameof(AuthFailure);
        /// <summary>Audit event type: session verified.</summary>
        public const string SessionVerified = nameof(SessionVerified);
        /// <summary>Audit event type: step-up verification succeeded.</summary>
        public const string StepUpVerified = nameof(StepUpVerified);
        /// <summary>Audit event type: step-up verification failed.</summary>
        public const string StepUpFailed = nameof(StepUpFailed);
        /// <summary>Audit event type: rate-limit lockout.</summary>
        public const string RateLimitLockout = nameof(RateLimitLockout);
        /// <summary>Audit event type: network tier changed.</summary>
        public const string NetworkTierChanged = nameof(NetworkTierChanged);
        /// <summary>Audit event type: software paths changed.</summary>
        public const string SoftwarePathsChanged = nameof(SoftwarePathsChanged);
        /// <summary>Audit event type: release channel changed.</summary>
        public const string ReleaseChannelChanged = nameof(ReleaseChannelChanged);
        /// <summary>Audit event type: super-admin action denied from a remote client.</summary>
        public const string SuperAdminActionDeniedRemote = nameof(SuperAdminActionDeniedRemote);
        /// <summary>Audit event type: certificate fingerprint mismatch.</summary>
        public const string CertificateFingerprintMismatch = nameof(CertificateFingerprintMismatch);
        /// <summary>Audit event type: tunnel started.</summary>
        public const string TunnelStarted = nameof(TunnelStarted);
        /// <summary>Audit event type: tunnel stopped.</summary>
        public const string TunnelStopped = nameof(TunnelStopped);
        /// <summary>Audit event type: Cloudflare API token configured.</summary>
        public const string CloudflareApiTokenConfigured = nameof(CloudflareApiTokenConfigured);
        /// <summary>Audit event type: Cloudflare tunnel configured.</summary>
        public const string CloudflareTunnelConfigured = nameof(CloudflareTunnelConfigured);
        /// <summary>Audit event type: Cloudflare disconnected.</summary>
        public const string CloudflareDisconnected = nameof(CloudflareDisconnected);
        /// <summary>Audit event type: internet server URL changed.</summary>
        public const string InternetServerUrlChanged = nameof(InternetServerUrlChanged);
        /// <summary>Audit event type: internal server URL changed.</summary>
        public const string InternalServerUrlChanged = nameof(InternalServerUrlChanged);
        /// <summary>Audit event type: server port changed.</summary>
        public const string ServerPortChanged = nameof(ServerPortChanged);
        /// <summary>Audit event type: certificate issued.</summary>
        public const string CertificateIssued = nameof(CertificateIssued);
        /// <summary>Audit event type: certificate reset.</summary>
        public const string CertificateReset = nameof(CertificateReset);
        /// <summary>Audit event type: provider rate limit hit.</summary>
        public const string ProviderRateLimitHit = nameof(ProviderRateLimitHit);
        /// <summary>Audit event type: provider API volume anomaly.</summary>
        public const string ProviderApiVolumeAnomaly = nameof(ProviderApiVolumeAnomaly);
        /// <summary>Audit event type: evidence exported.</summary>
        public const string EvidenceExported = nameof(EvidenceExported);
        /// <summary>Audit event type: backup exported.</summary>
        public const string BackupExported = nameof(BackupExported);
        /// <summary>Audit event type: backup imported.</summary>
        public const string BackupImported = nameof(BackupImported);
        /// <summary>Audit event type: setup administrator created.</summary>
        public const string SetupAdminCreated = nameof(SetupAdminCreated);
        /// <summary>Audit event type: lockout policy updated.</summary>
        public const string LockoutPolicyUpdated = nameof(LockoutPolicyUpdated);
        /// <summary>Audit event type: provider permissions updated.</summary>
        public const string ProviderPermissionsUpdated = nameof(ProviderPermissionsUpdated);
        /// <summary>Audit event type: settings permissions updated.</summary>
        public const string SettingsPermissionsUpdated = nameof(SettingsPermissionsUpdated);
        /// <summary>Audit event type: two-factor policy updated.</summary>
        public const string TwoFactorPolicyUpdated = nameof(TwoFactorPolicyUpdated);
        /// <summary>Audit event type: authentication methods updated.</summary>
        public const string AuthMethodsUpdated = nameof(AuthMethodsUpdated);
        /// <summary>Audit event type: operator unlocked.</summary>
        public const string OperatorUnlocked = nameof(OperatorUnlocked);
        /// <summary>Audit event type: super-admin password reset.</summary>
        public const string SuperAdminPasswordReset = nameof(SuperAdminPasswordReset);
        /// <summary>Audit event type: log viewed.</summary>
        public const string LogViewed = nameof(LogViewed);
        /// <summary>Audit event type: operator UI mode changed.</summary>
        public const string OperatorUiModeChanged = nameof(OperatorUiModeChanged);
        /// <summary>Audit event type: jamming incident recorded.</summary>
        public const string JammingIncidentRecorded = nameof(JammingIncidentRecorded);
        /// <summary>Audit event type: auto-ban created.</summary>
        public const string AutoBanCreated = nameof(AutoBanCreated);
        /// <summary>Audit event type: banned IP range removed.</summary>
        public const string BannedIpRangeRemoved = nameof(BannedIpRangeRemoved);
    }
}
