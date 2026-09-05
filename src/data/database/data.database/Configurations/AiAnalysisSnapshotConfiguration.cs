using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for AiAnalysisSnapshot entity.</summary>
    public class AiAnalysisSnapshotConfiguration : IEntityTypeConfiguration<AiAnalysisSnapshot>
    {
        public void Configure(EntityTypeBuilder<AiAnalysisSnapshot> builder)
        {
            _ = builder.HasKey(a => a.Id);

            _ = builder.Property(a => a.FullDescription)
                .HasMaxLength(2048);

            _ = builder.HasIndex(a => a.DownloadEventId);
        }
    }
}
