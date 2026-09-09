using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DetectionTypeOccurrence entity.</summary>
    public class DetectionTypeOccurrenceConfiguration : IEntityTypeConfiguration<DetectionTypeOccurrence>
    {
        public void Configure(EntityTypeBuilder<DetectionTypeOccurrence> builder)
        {
            _ = builder.HasKey(d => d.Id);

            _ = builder.Property(d => d.DetectionType)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(d => d.MediaItemDetectionId);
        }
    }
}
