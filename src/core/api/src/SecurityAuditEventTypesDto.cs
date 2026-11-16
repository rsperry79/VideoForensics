namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Security audit event type names for the client. Mirrors the data-layer SecurityAuditEventTypes constants.</summary>
    public static class SecurityAuditEventTypesDto
    {
        public const string PairingInitiated = nameof(PairingInitiated);
        public const string PairingCompleted = nameof(PairingCompleted);
        public const string PairingRevoked = nameof(PairingRevoked);
        public const string PairingRemoved = nameof(PairingRemoved);
        public const string DeviceCodeIssued = nameof(DeviceCodeIssued);
        public const string DeviceCodeApproved = nameof(DeviceCodeApproved);
        public const string DeviceCodeExpired = nameof(DeviceCodeExpired);
        public const string OperatorDeactivated = nameof(OperatorDeactivated);
        public const string OperatorApproved = nameof(OperatorApproved);
        public const string OperatorRegistered = nameof(OperatorRegistered);
        public const string PasswordReset = nameof(PasswordReset);
        public const string CredentialRegistered = nameof(CredentialRegistered);
        public const string CredentialApproved = nameof(CredentialApproved);
        public const string CredentialRevoked = nameof(CredentialRevoked);
        public const string AuthSuccess = nameof(AuthSuccess);
        public const string AuthFailure = nameof(AuthFailure);
        public const string SessionVerified = nameof(SessionVerified);
        public const string StepUpVerified = nameof(StepUpVerified);
        public const string StepUpFailed = nameof(StepUpFailed);
        public const string RateLimitLockout = nameof(RateLimitLockout);
        public const string NetworkTierChanged = nameof(NetworkTierChanged);
        public const string SoftwarePathsChanged = nameof(SoftwarePathsChanged);
        public const string ReleaseChannelChanged = nameof(ReleaseChannelChanged);
        public const string SuperAdminActionDeniedRemote = nameof(SuperAdminActionDeniedRemote);
        public const string CertificateFingerprintMismatch = nameof(CertificateFingerprintMismatch);
        public const string TunnelStarted = nameof(TunnelStarted);
        public const string TunnelStopped = nameof(TunnelStopped);
        public const string CloudflareApiTokenConfigured = nameof(CloudflareApiTokenConfigured);
        public const string CloudflareTunnelConfigured = nameof(CloudflareTunnelConfigured);
        public const string CloudflareDisconnected = nameof(CloudflareDisconnected);
        public const string InternetServerUrlChanged = nameof(InternetServerUrlChanged);
        public const string InternalServerUrlChanged = nameof(InternalServerUrlChanged);
        public const string ServerPortChanged = nameof(ServerPortChanged);
        public const string CertificateIssued = nameof(CertificateIssued);
        public const string CertificateReset = nameof(CertificateReset);
        public const string ProviderRateLimitHit = nameof(ProviderRateLimitHit);
        public const string ProviderApiVolumeAnomaly = nameof(ProviderApiVolumeAnomaly);
        public const string EvidenceExported = nameof(EvidenceExported);
        public const string BackupExported = nameof(BackupExported);
        public const string BackupImported = nameof(BackupImported);
        public const string SetupAdminCreated = nameof(SetupAdminCreated);
        public const string LockoutPolicyUpdated = nameof(LockoutPolicyUpdated);
        public const string ProviderPermissionsUpdated = nameof(ProviderPermissionsUpdated);
        public const string SettingsPermissionsUpdated = nameof(SettingsPermissionsUpdated);
        public const string TwoFactorPolicyUpdated = nameof(TwoFactorPolicyUpdated);
        public const string AuthMethodsUpdated = nameof(AuthMethodsUpdated);
        public const string OperatorUnlocked = nameof(OperatorUnlocked);
        public const string SuperAdminPasswordReset = nameof(SuperAdminPasswordReset);
        public const string LogViewed = nameof(LogViewed);
        public const string OperatorUiModeChanged = nameof(OperatorUiModeChanged);
        public const string JammingIncidentRecorded = nameof(JammingIncidentRecorded);
        public const string AutoBanCreated = nameof(AutoBanCreated);
        public const string BannedIpRangeRemoved = nameof(BannedIpRangeRemoved);
    }
}
