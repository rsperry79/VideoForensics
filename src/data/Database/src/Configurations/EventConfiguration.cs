using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Event entity.</summary>
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(e => e.SnapshotUrl)
                .HasMaxLength(1024);

            _ = builder.Property(e => e.RecordingStatus)
                .HasMaxLength(256);

            _ = builder.HasIndex(e => new { e.DeviceId, e.ProviderEventId })
                .IsUnique();

            _ = builder.HasIndex(e => e.DeviceId);

            _ = builder.HasIndex(e => e.EventDetectionId);
        }
    }
}
