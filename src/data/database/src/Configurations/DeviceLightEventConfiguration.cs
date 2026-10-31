using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceLightEvent.</summary>
    public class DeviceLightEventConfiguration : IEntityTypeConfiguration<DeviceLightEvent>
    {
        public void Configure(EntityTypeBuilder<DeviceLightEvent> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.HasOne<Device>()
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Per-device lookups read the latest event, so the composite index serves both filter and sort.
            _ = builder.HasIndex(e => new { e.DeviceId, e.OccurredAtUtc });
        }
    }
}
