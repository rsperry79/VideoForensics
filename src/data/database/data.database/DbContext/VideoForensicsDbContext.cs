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

        /// <summary>Gets or sets the download events.</summary>
        public DbSet<DownloadEvent> DownloadEvents { get; set; }

        /// <summary>Gets or sets the device health snapshots.</summary>
        public DbSet<DeviceHealthSnapshot> DeviceHealthSnapshots { get; set; }

        /// <summary>Gets or sets the AI analysis snapshots.</summary>
        public DbSet<AiAnalysisSnapshot> AiAnalysisSnapshots { get; set; }

        /// <summary>Gets or sets the credentials.</summary>
        public DbSet<Credential> Credentials { get; set; }

        /// <summary>Gets or sets the action log entries.</summary>
        public DbSet<ActionLogEntry> ActionLogEntries { get; set; }

        /// <summary>Gets or sets the integrity records.</summary>
        public DbSet<IntegrityRecord> IntegrityRecords { get; set; }

        /// <summary>Gets or sets the events.</summary>
        public DbSet<Event> Events { get; set; }

        /// <summary>Gets or sets the device configuration snapshots.</summary>
        public DbSet<DeviceConfigSnapshot> DeviceConfigSnapshots { get; set; }

        /// <summary>Gets or sets the annotations.</summary>
        public DbSet<Annotation> Annotations { get; set; }

        /// <summary>Gets or sets the provider reconciliation records.</summary>
        public DbSet<ProviderReconciliationRecord> ProviderReconciliationRecords { get; set; }

        /// <summary>Gets or sets the export records.</summary>
        public DbSet<ExportRecord> ExportRecords { get; set; }

        /// <summary>Gets or sets the export record items.</summary>
        public DbSet<ExportRecordItem> ExportRecordItems { get; set; }

        /// <summary>Gets or sets the application settings.</summary>
        public DbSet<AppSetting> AppSettings { get; set; }

        /// <summary>Configures the model using entity configurations from this assembly.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(VideoForensicsDbContext).Assembly);
        }
    }
}
