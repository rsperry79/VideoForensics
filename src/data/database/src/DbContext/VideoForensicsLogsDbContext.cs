using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Configurations;

namespace VideoForensics.Data.Database.DbContext
{
    /// <summary>
    /// Operational logs kept in logs.db, apart from the audit database: provider error rows, provider call records, and
    /// live-view telemetry. Nothing here is hash-chained or needed for evidence, so it can be pruned or rebuilt. Telemetry
    /// samples have no foreign key to live-view sessions, which live in the main database.
    /// </summary>
    public class VideoForensicsLogsDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public VideoForensicsLogsDbContext(DbContextOptions<VideoForensicsLogsDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProviderApiErrorLog> ProviderApiErrorLogs { get; set; }

        public DbSet<ProviderApiCallRecord> ProviderApiCallRecords { get; set; }

        public DbSet<LiveViewTelemetrySample> LiveViewTelemetrySamples { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new ProviderApiErrorLogConfiguration());
            modelBuilder.ApplyConfiguration(new ProviderApiCallRecordConfiguration());

            // The main configuration adds a foreign key to LiveViewSession, which is not part of this model.
            _ = modelBuilder.Entity<LiveViewTelemetrySample>(builder =>
            {
                _ = builder.HasKey(t => t.Id);
                _ = builder.HasIndex(t => new { t.SessionId, t.CapturedAtUtc });
            });
        }
    }
}
