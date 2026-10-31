using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingDeviceAdvancedMotionSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceAdvancedMotionSettingsConfiguration : IEntityTypeConfiguration<RingDeviceAdvancedMotionSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceAdvancedMotionSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceAdvancedMotionSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceBackendSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceBackendSettingsConfiguration : IEntityTypeConfiguration<RingDeviceBackendSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceBackendSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceBackendSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceClientDeviceSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceClientDeviceSettingsConfiguration : IEntityTypeConfiguration<RingDeviceClientDeviceSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceClientDeviceSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceClientDeviceSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceCvSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceCvSettingsConfiguration : IEntityTypeConfiguration<RingDeviceCvSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceCvSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceCvSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceGeneralSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceGeneralSettingsConfiguration : IEntityTypeConfiguration<RingDeviceGeneralSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceGeneralSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceGeneralSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceMiscSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceMiscSettingsConfiguration : IEntityTypeConfiguration<RingDeviceMiscSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceMiscSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceMiscSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceMotionSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceMotionSettingsConfiguration : IEntityTypeConfiguration<RingDeviceMotionSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceMotionSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceMotionSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceSnapshotSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceSnapshotSettingsConfiguration : IEntityTypeConfiguration<RingDeviceSnapshotSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceSnapshotSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceSnapshotSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceStreamSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceStreamSettingsConfiguration : IEntityTypeConfiguration<RingDeviceStreamSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceStreamSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceStreamSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceVideoSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceVideoSettingsConfiguration : IEntityTypeConfiguration<RingDeviceVideoSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceVideoSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceVideoSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceVodSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceVodSettingsConfiguration : IEntityTypeConfiguration<RingDeviceVodSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceVodSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceVodSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceMotionSnoozeProfile. Rows are keyed by device and replaced per sync, deleted with the device.</summary>
    public class RingDeviceMotionSnoozeProfileConfiguration : IEntityTypeConfiguration<RingDeviceMotionSnoozeProfile>
    {
        public void Configure(EntityTypeBuilder<RingDeviceMotionSnoozeProfile> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.HasIndex(p => p.DeviceId);
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceCvThresholdOverrideVersion. Rows are keyed by device and replaced per sync, deleted with the device.</summary>
    public class RingDeviceCvThresholdOverrideVersionConfiguration : IEntityTypeConfiguration<RingDeviceCvThresholdOverrideVersion>
    {
        public void Configure(EntityTypeBuilder<RingDeviceCvThresholdOverrideVersion> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.HasIndex(p => p.DeviceId);
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceCvThresholdOverrideValue. Rows are keyed by device and replaced per sync, deleted with the device.</summary>
    public class RingDeviceCvThresholdOverrideValueConfiguration : IEntityTypeConfiguration<RingDeviceCvThresholdOverrideValue>
    {
        public void Configure(EntityTypeBuilder<RingDeviceCvThresholdOverrideValue> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.HasIndex(p => p.DeviceId);
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
