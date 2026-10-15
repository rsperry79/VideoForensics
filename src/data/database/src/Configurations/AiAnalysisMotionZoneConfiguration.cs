using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for AiAnalysisMotionZone entity.</summary>
    public class AiAnalysisMotionZoneConfiguration : IEntityTypeConfiguration<AiAnalysisMotionZone>
    {
        public void Configure(EntityTypeBuilder<AiAnalysisMotionZone> builder)
        {
            _ = builder.HasKey(a => a.Id);

            _ = builder.Property(a => a.ZoneId)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(a => a.ZoneName)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.Confidence)
                .HasPrecision(5, 4);

            _ = builder.HasIndex(a => a.AiAnalysisSnapshotId);

            _ = builder.HasIndex(a => new { a.AiAnalysisSnapshotId, a.ZoneId })
                .IsUnique();

            _ = builder.HasOne(a => a.AiAnalysisSnapshot)
                .WithMany(s => s.MotionZones)
                .HasForeignKey(a => a.AiAnalysisSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
