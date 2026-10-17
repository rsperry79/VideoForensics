using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.DbContext
{
    /// <summary>EF Core DbContext for VideoForensics data access layer.</summary>
    public class VideoForensicsDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        /// <summary>Initializes a new instance of the VideoForensicsDbContext.</summary>
        public VideoForensicsDbContext(DbContextOptions<VideoForensicsDbContext> options)
            : base(options)
        {
        }

        /// <summary>Gets or sets the users.</summary>
        public DbSet<User> Users { get; set; }

        /// <summary>Gets or sets the provider accounts.</summary>
        public DbSet<ProviderAccount> ProviderAccounts { get; set; }

        /// <summary>Gets or sets the sync schedules.</summary>
        public DbSet<SyncSchedule> SyncSchedules { get; set; }

        /// <summary>Gets or sets the jamming schedule windows.</summary>
        public DbSet<JammingScheduleWindow> JammingScheduleWindows { get; set; }

        /// <summary>Gets or sets the locations.</summary>
        public DbSet<Location> Locations { get; set; }

        /// <summary>Gets or sets the provider account to location links.</summary>
        public DbSet<ProviderAccountLocation> ProviderAccountLocations { get; set; }

        /// <summary>Gets or sets the devices.</summary>
        public DbSet<Device> Devices { get; set; }

        /// <summary>Gets or sets the media items.</summary>
        public DbSet<MediaItem> MediaItems { get; set; }

        /// <summary>Gets or sets the media item detections.</summary>
        public DbSet<MediaItemDetection> MediaItemDetections { get; set; }

        /// <summary>Gets or sets the detection type occurrences.</summary>
        public DbSet<DetectionTypeOccurrence> DetectionTypeOccurrences { get; set; }

        /// <summary>Gets or sets the detected persons.</summary>
        public DbSet<DetectedPerson> DetectedPersons { get; set; }

        /// <summary>Gets or sets the download events.</summary>
        public DbSet<DownloadEvent> DownloadEvents { get; set; }

        /// <summary>Gets or sets the AI analysis snapshots.</summary>
        public DbSet<AiAnalysisSnapshot> AiAnalysisSnapshots { get; set; }

        /// <summary>Gets or sets the AI analysis tags.</summary>
        public DbSet<AiAnalysisTag> AiAnalysisTags { get; set; }

        /// <summary>Gets or sets the AI analysis motion zones.</summary>
        public DbSet<AiAnalysisMotionZone> AiAnalysisMotionZones { get; set; }

        /// <summary>Gets or sets the credentials.</summary>
        public DbSet<Credential> Credentials { get; set; }

        /// <summary>Gets or sets the action log entries.</summary>
        public DbSet<ActionLogEntry> ActionLogEntries { get; set; }

        /// <summary>Gets or sets the integrity records.</summary>
        public DbSet<IntegrityRecord> IntegrityRecords { get; set; }

        /// <summary>Gets or sets the legal holds.</summary>
        public DbSet<LegalHold> LegalHolds { get; set; }

        /// <summary>Gets or sets the events.</summary>
        public DbSet<Event> Events { get; set; }

        /// <summary>Gets or sets the event detections.</summary>
        public DbSet<EventDetection> EventDetections { get; set; }

        /// <summary>Gets or sets the event detection zones.</summary>
        public DbSet<EventDetectionZone> EventDetectionZones { get; set; }

        /// <summary>Gets or sets the event detection type occurrences.</summary>
        public DbSet<EventDetectionTypeOccurrence> EventDetectionTypeOccurrences { get; set; }

        /// <summary>Gets or sets the event security alerts.</summary>
        public DbSet<EventSecurityAlert> EventSecurityAlerts { get; set; }

        /// <summary>Gets or sets the event detected persons.</summary>
        public DbSet<EventDetectedPerson> EventDetectedPersons { get; set; }

        /// <summary>Gets or sets the device configuration snapshots.</summary>
        public DbSet<DeviceConfigSnapshot> DeviceConfigSnapshots { get; set; }

        /// <summary>Gets or sets the provider reconciliation records.</summary>
        public DbSet<ProviderReconciliationRecord> ProviderReconciliationRecords { get; set; }

        /// <summary>Gets or sets the export records.</summary>
        public DbSet<ExportRecord> ExportRecords { get; set; }

        /// <summary>Gets or sets the export record items.</summary>
        public DbSet<ExportRecordItem> ExportRecordItems { get; set; }

        /// <summary>Gets or sets the application settings.</summary>
        public DbSet<AppSetting> AppSettings { get; set; }

        /// <summary>Gets or sets the jamming incident records.</summary>
        public DbSet<JammingIncidentRecord> JammingIncidentRecords { get; set; }

        /// <summary>Gets or sets the jamming stats summaries.</summary>
        public DbSet<JammingStatsSummary> JammingStatsSummaries { get; set; }

        /// <summary>Gets or sets the Ring account records.</summary>
        public DbSet<RingAccount> RingAccounts { get; set; }

        /// <summary>Gets or sets the Ring account feature records.</summary>
        public DbSet<RingAccountFeatures> RingAccountFeatures { get; set; }

        /// <summary>Gets or sets the Ring account profiles (every field except the authentication token).</summary>
        public DbSet<RingAccountProfile> RingAccountProfiles { get; set; }

        /// <summary>Gets or sets the Ring session feature flags of a profile, typed columns only.</summary>
        public DbSet<RingAccountSessionFeatures> RingAccountSessionFeatures { get; set; }

        /// <summary>Gets or sets the profile flags that have no dedicated column.</summary>
        public DbSet<RingAccountProfileFlag> RingAccountProfileFlags { get; set; }

        /// <summary>Gets or sets the array elements of profile flags.</summary>
        public DbSet<RingAccountProfileFlagItem> RingAccountProfileFlagItems { get; set; }

        /// <summary>Gets or sets the Ring chime ringtones.</summary>
        public DbSet<RingAccountRingtone> RingAccountRingtones { get; set; }

        /// <summary>Gets or sets the device kinds each ringtone supports.</summary>
        public DbSet<RingAccountRingtoneDeviceKind> RingAccountRingtoneDeviceKinds { get; set; }

        /// <summary>Gets or sets the default ringtone ids of a Ring account.</summary>
        public DbSet<RingAccountRingtoneDefaults> RingAccountRingtoneDefaults { get; set; }

        /// <summary>Gets or sets the Smart Lighting group sets, one per location.</summary>
        public DbSet<LightGroupSet> LightGroupSets { get; set; }

        /// <summary>Gets or sets the Smart Lighting groups.</summary>
        public DbSet<LightGroup> LightGroups { get; set; }

        /// <summary>Gets or sets the device ids of Smart Lighting groups.</summary>
        public DbSet<LightGroupDevice> LightGroupDevices { get; set; }

        /// <summary>Gets or sets the current location modes.</summary>
        public DbSet<RingLocationMode> RingLocationModes { get; set; }

        /// <summary>Gets or sets the raw notYetParticipatingInMode elements of location modes.</summary>
        public DbSet<RingLocationModeNotYetParticipating> RingLocationModeNotYetParticipating { get; set; }

        /// <summary>Gets or sets the location mode settings, one per location.</summary>
        public DbSet<RingLocationModeSettings> RingLocationModeSettings { get; set; }

        /// <summary>Gets or sets the supported action groups of location mode settings.</summary>
        public DbSet<RingLocationModeSupportedActionGroup> RingLocationModeSupportedActionGroups { get; set; }

        /// <summary>Gets or sets the per-mode device entries of location mode settings.</summary>
        public DbSet<RingLocationModeSettingsEntry> RingLocationModeSettingsEntries { get; set; }

        /// <summary>Gets or sets the enabled and supported actions of location mode entries.</summary>
        public DbSet<RingLocationModeSettingsEntryAction> RingLocationModeSettingsEntryActions { get; set; }

        /// <summary>Gets or sets the location mode sharing state, one per location.</summary>
        public DbSet<RingLocationModeSharing> RingLocationModeSharings { get; set; }

        /// <summary>Gets or sets the device capabilities.</summary>
        public DbSet<DeviceCapabilities> DeviceCapabilities { get; set; }

        /// <summary>Gets or sets the device health metrics.</summary>
        public DbSet<DeviceHealth> DeviceHealths { get; set; }

        /// <summary>Gets or sets the device features.</summary>
        public DbSet<DeviceFeatures> DeviceFeatures { get; set; }

        /// <summary>Gets or sets the device alerts.</summary>
        public DbSet<DeviceAlerts> DeviceAlerts { get; set; }

        /// <summary>Gets or sets the device motion snooze presets.</summary>
        public DbSet<DeviceMotionSnoozePreset> DeviceMotionSnoozePresets { get; set; }

        /// <summary>Gets or sets the device live view presets.</summary>
        public DbSet<DeviceLiveViewPreset> DeviceLiveViewPresets { get; set; }

        /// <summary>Gets or sets the device advanced motion zones.</summary>
        public DbSet<DeviceAdvancedMotionZone> DeviceAdvancedMotionZones { get; set; }

        /// <summary>Gets or sets the device advanced motion zone vertices.</summary>
        public DbSet<DeviceAdvancedMotionZoneVertex> DeviceAdvancedMotionZoneVertices { get; set; }

        /// <summary>Gets or sets the device motion and package zones.</summary>
        public DbSet<DeviceZone> DeviceZones { get; set; }

        /// <summary>Gets or sets the detection types of device zones.</summary>
        public DbSet<DeviceZoneDetectionType> DeviceZoneDetectionTypes { get; set; }

        /// <summary>Gets or sets the vertices of device zones.</summary>
        public DbSet<DeviceZoneVertex> DeviceZoneVertices { get; set; }

        /// <summary>Gets or sets the advanced object settings of a device.</summary>
        public DbSet<DeviceAdvancedObjectSetting> DeviceAdvancedObjectSettings { get; set; }

        /// <summary>Gets or sets the supported RPC command rows of device health readings.</summary>
        public DbSet<DeviceHealthRpcCommand> DeviceHealthRpcCommands { get; set; }

        /// <summary>Gets or sets the location metadata.</summary>
        public DbSet<LocationMetadata> LocationMetadata { get; set; }

        /// <summary>Gets or sets the access audit logs.</summary>
        public DbSet<AccessAuditLogEntity> AccessAuditLogs { get; set; }

        /// <summary>Gets or sets the export audit records.</summary>
        public DbSet<ExportAuditRecordEntity> ExportAuditRecords { get; set; }

        /// <summary>Gets or sets the Operators (plan §5.11).</summary>
        public DbSet<Operator> Operators { get; set; }

        /// <summary>Gets or sets the operator credentials (WebAuthn passkeys for operator login).</summary>
        public DbSet<OperatorCredential> OperatorCredentials { get; set; } = null!;

        /// <summary>Gets or sets the paired devices (plan §5.1/§5.4).</summary>
        public DbSet<PairedDevice> PairedDevices { get; set; }

        /// <summary>Gets or sets the security audit log entries (plan §5.5).</summary>
        public DbSet<SecurityAuditLogEntry> SecurityAuditLogEntries { get; set; }

        /// <summary>Gets or sets the provider API call log (plan §5.12).</summary>
        public DbSet<ProviderApiCallRecord> ProviderApiCallRecords { get; set; }

        /// <summary>Gets or sets the provider API error log (status code + truncated response body per failed download attempt).</summary>
        public DbSet<ProviderApiErrorLog> ProviderApiErrorLogs { get; set; }

        /// <summary>Gets or sets the per-operator UI preferences (theme mode, language).</summary>
        public DbSet<OperatorPreferences> OperatorPreferences { get; set; }

        /// <summary>Gets or sets the notices (persisted notification events for audit trail and history).</summary>
        public DbSet<Notice> Notices { get; set; }

        /// <summary>Gets or sets the notice dismissals (per-operator suppression of notices).</summary>
        public DbSet<NoticeDismissal> NoticeDismissals { get; set; }

        /// <summary>Gets or sets the per-operator push notification preferences.</summary>
        public DbSet<OperatorNotificationPreference> OperatorNotificationPreferences { get; set; }

        /// <summary>Gets or sets the push subscriptions (Web Push API credentials per browser/device).</summary>
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        /// <summary>Gets or sets the lockout policy settings (Phase 0 security hardening).</summary>
        public DbSet<LockoutPolicySettings> LockoutPolicySettings { get; set; }

        /// <summary>Gets or sets the two-factor role requirements (Phase 0 security hardening).</summary>
        public DbSet<TwoFactorRoleRequirement> TwoFactorRoleRequirements { get; set; }

        /// <summary>Gets or sets the banned IP ranges (Phase 0 security hardening).</summary>
        public DbSet<BannedIpRange> BannedIpRanges { get; set; }

        /// <summary>Gets or sets the security events (Phase 0.5 login/breach tracking).</summary>
        public DbSet<SecurityEvent> SecurityEvents { get; set; }

        /// <summary>Gets or sets the live-view sessions.</summary>
        public DbSet<LiveViewSession> LiveViewSessions { get; set; }

        /// <summary>Gets or sets the live-view telemetry samples.</summary>
        public DbSet<LiveViewTelemetrySample> LiveViewTelemetrySamples { get; set; }

        /// <summary>Gets or sets the camera bitrate baselines.</summary>
        public DbSet<CameraBitrateBaseline> CameraBitrateBaselines { get; set; }

        /// <summary>Gets or sets the forensic cases with pinned evidence and chain-of-custody tracking.</summary>
        public DbSet<ForensicCase> Cases { get; set; }

        /// <summary>Gets or sets the case device scope junctions.</summary>
        public DbSet<CaseDevice> CaseDevices { get; set; }

        /// <summary>Gets or sets the evidence items pinned to cases (events and media).</summary>
        public DbSet<CaseItem> CaseItems { get; set; }

        /// <summary>Gets or sets the derived-still capture provenance rows (Evidence "grab still with hash").</summary>
        public DbSet<MediaStillCapture> MediaStillCaptures { get; set; }

        /// <summary>Gets or sets the alerts triggered by jamming detection.</summary>
        public DbSet<Alert> Alerts { get; set; }

        /// <summary>Gets or sets one row per account sync attempt (manual or initial).</summary>
        public DbSet<ProviderSyncRun> ProviderSyncRuns { get; set; }

        /// <summary>Configures the model using entity configurations from this assembly.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(VideoForensicsDbContext).Assembly);
        }
    }
}
