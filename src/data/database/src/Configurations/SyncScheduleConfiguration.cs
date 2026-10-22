using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for SyncSchedule entity.</summary>
    public class SyncScheduleConfiguration : IEntityTypeConfiguration<SyncSchedule>
    {
        public void Configure(EntityTypeBuilder<SyncSchedule> builder)
        {
            _ = builder.HasKey(ss => ss.Id);

            _ = builder.HasIndex(ss => ss.ProviderAccountId).IsUnique();

            // Null = not configured; the pacer's default spacing applies.
            _ = builder.Property(ss => ss.SnapshotIntervalSeconds).IsRequired(false);

            // Matches the entity default, so a row written without the value (or added by migration) gets 15, not 0.
            _ = builder.Property(ss => ss.SnapshotRollupIntervalMinutes).HasDefaultValue(SyncSchedule.DefaultSnapshotRollupIntervalMinutes);

            // Configure relationship to JammingScheduleWindows with cascade delete
            _ = builder
                .HasMany(ss => ss.JammingWindows)
                .WithOne()
                .HasForeignKey(jsw => jsw.SyncScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
