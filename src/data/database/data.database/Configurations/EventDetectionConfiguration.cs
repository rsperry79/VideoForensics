using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for EventDetection entity.</summary>
    public class EventDetectionConfiguration : IEntityTypeConfiguration<EventDetection>
    {
        public void Configure(EntityTypeBuilder<EventDetection> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.DetectionType)
                .HasMaxLength(256);

            _ = builder.Property(e => e.FullDescription)
                .HasMaxLength(2048);

            _ = builder.Property(e => e.ShortDescription)
                .HasMaxLength(512);

            _ = builder.Property(e => e.ModelVersion)
                .HasMaxLength(64);

            _ = builder.HasIndex(e => e.EventId);
        }
    }
}
