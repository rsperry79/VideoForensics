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

        /// <summary>Gets or sets the locations.</summary>
        public DbSet<Location> Locations { get; set; }

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

        /// <summary>Gets or sets the device health snapshots.</summary>
        public DbSet<DeviceHealthSnapshot> DeviceHealthSnapshots { get; set; }

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

        /// <summary>Gets or sets the device capabilities.</summary>
        public DbSet<DeviceCapabilities> DeviceCapabilities { get; set; }

        /// <summary>Gets or sets the device health metrics.</summary>
        public DbSet<DeviceHealth> DeviceHealths { get; set; }

        /// <summary>Gets or sets the device features.</summary>
        public DbSet<DeviceFeatures> DeviceFeatures { get; set; }

        /// <summary>Gets or sets the device alerts.</summary>
        public DbSet<DeviceAlerts> DeviceAlerts { get; set; }

        /// <summary>Gets or sets the location metadata.</summary>
        public DbSet<LocationMetadata> LocationMetadata { get; set; }

        /// <summary>Gets or sets the access audit logs.</summary>
        public DbSet<AccessAuditLogEntity> AccessAuditLogs { get; set; }

        /// <summary>Gets or sets the export audit records.</summary>
        public DbSet<ExportAuditRecordEntity> ExportAuditRecords { get; set; }



        /// <summary>Gets or sets the Operators (plan §5.11).</summary>
        public DbSet<Operator> Operators { get; set; }

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

        /// <summary>Configures the model using entity configurations from this assembly.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(VideoForensicsDbContext).Assembly);
        }
    }
}
