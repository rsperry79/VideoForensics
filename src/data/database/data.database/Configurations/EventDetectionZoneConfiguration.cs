using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for EventDetectionZone entity.</summary>
    public class EventDetectionZoneConfiguration : IEntityTypeConfiguration<EventDetectionZone>
    {
        public void Configure(EntityTypeBuilder<EventDetectionZone> builder)
        {
            _ = builder.HasKey(d => d.Id);

            _ = builder.Property(d => d.ZoneId)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(d => d.ZoneName)
                .HasMaxLength(256);

            _ = builder.HasIndex(d => new { d.EventDetectionId, d.ZoneId });
        }
    }
}
