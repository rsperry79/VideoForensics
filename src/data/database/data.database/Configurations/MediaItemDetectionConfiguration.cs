using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for MediaItemDetection entity.</summary>
    public class MediaItemDetectionConfiguration : IEntityTypeConfiguration<MediaItemDetection>
    {
        public void Configure(EntityTypeBuilder<MediaItemDetection> builder)
        {
            _ = builder.HasKey(m => m.Id);

            _ = builder.Property(m => m.DetectionType)
                .HasMaxLength(256);

            _ = builder.Property(m => m.FullDescription)
                .HasMaxLength(2048);

            _ = builder.Property(m => m.ShortDescription)
                .HasMaxLength(512);

            _ = builder.Property(m => m.ModelVersion)
                .HasMaxLength(64);

            _ = builder.HasIndex(m => m.MediaItemId);
        }
    }
}
