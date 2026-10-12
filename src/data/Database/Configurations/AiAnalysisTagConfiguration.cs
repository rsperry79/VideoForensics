using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for AiAnalysisTag entity.</summary>
    public class AiAnalysisTagConfiguration : IEntityTypeConfiguration<AiAnalysisTag>
    {
        public void Configure(EntityTypeBuilder<AiAnalysisTag> builder)
        {
            _ = builder.HasKey(a => a.Id);

            _ = builder.Property(a => a.TagName)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(a => a.AiAnalysisSnapshotId);

            _ = builder.HasIndex(a => new { a.AiAnalysisSnapshotId, a.TagName })
                .IsUnique();

            _ = builder.HasOne(a => a.AiAnalysisSnapshot)
                .WithMany(s => s.Tags)
                .HasForeignKey(a => a.AiAnalysisSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
