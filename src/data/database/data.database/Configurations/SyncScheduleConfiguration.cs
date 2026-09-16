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

            // Configure relationship to JammingScheduleWindows with cascade delete
            _ = builder
                .HasMany(ss => ss.JammingWindows)
                .WithOne()
                .HasForeignKey(jsw => jsw.SyncScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
