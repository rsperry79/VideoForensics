using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for EventDetectedPerson entity.</summary>
    public class EventDetectedPersonConfiguration : IEntityTypeConfiguration<EventDetectedPerson>
    {
        public void Configure(EntityTypeBuilder<EventDetectedPerson> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.ProfileId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(e => e.ProfileName)
                .HasMaxLength(512);

            _ = builder.Property(e => e.ThumbnailUrl)
                .HasMaxLength(1024);

            _ = builder.HasIndex(e => e.EventId);
        }
    }
}
