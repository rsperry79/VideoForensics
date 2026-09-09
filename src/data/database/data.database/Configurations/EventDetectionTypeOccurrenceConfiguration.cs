using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for EventDetectionTypeOccurrence entity.</summary>
    public class EventDetectionTypeOccurrenceConfiguration : IEntityTypeConfiguration<EventDetectionTypeOccurrence>
    {
        public void Configure(EntityTypeBuilder<EventDetectionTypeOccurrence> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.DetectionType)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(e => e.EventDetectionId);
        }
    }
}
