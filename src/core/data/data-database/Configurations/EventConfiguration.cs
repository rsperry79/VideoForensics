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
            builder.HasKey(e => e.Id);

            builder.Property(e => e.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(e => e.SnapshotUrl)
                .HasMaxLength(1024);

            builder.HasIndex(e => new { e.DeviceId, e.ProviderEventId })
                .IsUnique();

            builder.HasIndex(e => e.DeviceId);
        }
    }
}
