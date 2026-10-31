using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for a Ring ignore zone; one row per zone number per device, removed with the device.</summary>
    public class RingDeviceIgnoreZoneConfiguration : IEntityTypeConfiguration<RingDeviceIgnoreZone>
    {
        public void Configure(EntityTypeBuilder<RingDeviceIgnoreZone> builder)
        {
            _ = builder.HasKey(z => z.Id);
            _ = builder.Property(z => z.Name).HasMaxLength(256);
            _ = builder.HasIndex(z => new { z.DeviceId, z.ZoneNumber }).IsUnique();
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(z => z.DeviceId).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasMany(z => z.Vertices).WithOne().HasForeignKey(v => v.RingDeviceIgnoreZoneId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for a Ring ignore zone vertex; one row per position per zone.</summary>
    public class RingDeviceIgnoreZoneVertexConfiguration : IEntityTypeConfiguration<RingDeviceIgnoreZoneVertex>
    {
        public void Configure(EntityTypeBuilder<RingDeviceIgnoreZoneVertex> builder)
        {
            _ = builder.HasKey(v => v.Id);
            _ = builder.HasIndex(v => new { v.RingDeviceIgnoreZoneId, v.Position }).IsUnique();
        }
    }

    /// <summary>Fluent API configuration for one entry of a device's Ring motion_zones array, ordered by Position.</summary>
    public class RingDeviceMotionZoneConfiguration : IEntityTypeConfiguration<RingDeviceMotionZone>
    {
        public void Configure(EntityTypeBuilder<RingDeviceMotionZone> builder)
        {
            _ = builder.HasKey(z => z.Id);
            _ = builder.HasIndex(z => new { z.DeviceId, z.Position }).IsUnique();
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(z => z.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one entry of a device's Ring advanced_motion_detection_types array, ordered by Position.</summary>
    public class RingDeviceAdvancedMotionDetectionTypeConfiguration : IEntityTypeConfiguration<RingDeviceAdvancedMotionDetectionType>
    {
        public void Configure(EntityTypeBuilder<RingDeviceAdvancedMotionDetectionType> builder)
        {
            _ = builder.HasKey(t => t.Id);
            _ = builder.Property(t => t.DetectionType).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(t => new { t.DeviceId, t.Position }).IsUnique();
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(t => t.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
