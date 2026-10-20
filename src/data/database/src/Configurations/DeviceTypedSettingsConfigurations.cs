using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceOfflineMotionSettings. At most one row per device, replaced per settings write.</summary>
    public class DeviceOfflineMotionSettingsConfiguration : IEntityTypeConfiguration<DeviceOfflineMotionSettings>
    {
        public void Configure(EntityTypeBuilder<DeviceOfflineMotionSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId);
        }
    }

    /// <summary>Fluent API configuration for DeviceDetectionType. One row per CV detection type per device.</summary>
    public class DeviceDetectionTypeConfiguration : IEntityTypeConfiguration<DeviceDetectionType>
    {
        public void Configure(EntityTypeBuilder<DeviceDetectionType> builder)
        {
            _ = builder.HasKey(t => t.Id);
            _ = builder.Property(t => t.DetectionType).IsRequired().HasMaxLength(64);
            _ = builder.Property(t => t.Mode).HasMaxLength(32);
            _ = builder.HasIndex(t => t.DeviceId);
        }
    }

    /// <summary>Fluent API configuration for DeviceDetectionThreshold. One row per numeric threshold per device.</summary>
    public class DeviceDetectionThresholdConfiguration : IEntityTypeConfiguration<DeviceDetectionThreshold>
    {
        public void Configure(EntityTypeBuilder<DeviceDetectionThreshold> builder)
        {
            _ = builder.HasKey(t => t.Id);
            _ = builder.Property(t => t.DetectionType).IsRequired().HasMaxLength(64);
            _ = builder.HasIndex(t => t.DeviceId);
        }
    }
}
