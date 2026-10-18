using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceMotionSnoozePreset. Rows are keyed by device and replaced per sync.</summary>
    public class DeviceMotionSnoozePresetConfiguration : IEntityTypeConfiguration<DeviceMotionSnoozePreset>
    {
        public void Configure(EntityTypeBuilder<DeviceMotionSnoozePreset> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.Property(p => p.Preset).IsRequired().HasMaxLength(256);
            _ = builder.HasIndex(p => p.DeviceId);
        }
    }

    /// <summary>Fluent API configuration for DeviceLiveViewPreset. Rows are keyed by device and replaced per sync.</summary>
    public class DeviceLiveViewPresetConfiguration : IEntityTypeConfiguration<DeviceLiveViewPreset>
    {
        public void Configure(EntityTypeBuilder<DeviceLiveViewPreset> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.Property(p => p.Preset).IsRequired().HasMaxLength(256);
            _ = builder.HasIndex(p => p.DeviceId);
        }
    }

    /// <summary>Fluent API configuration for DeviceAdvancedMotionZone and its vertices.</summary>
    public class DeviceAdvancedMotionZoneConfiguration : IEntityTypeConfiguration<DeviceAdvancedMotionZone>
    {
        public void Configure(EntityTypeBuilder<DeviceAdvancedMotionZone> builder)
        {
            _ = builder.HasKey(z => z.Id);
            _ = builder.Property(z => z.ZoneKey).IsRequired().HasMaxLength(32);
            _ = builder.Property(z => z.Name).HasMaxLength(256);
            _ = builder.HasIndex(z => z.DeviceId);
            _ = builder.HasMany(z => z.Vertices).WithOne().HasForeignKey(v => v.DeviceAdvancedMotionZoneId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for DeviceAdvancedMotionZoneVertex.</summary>
    public class DeviceAdvancedMotionZoneVertexConfiguration : IEntityTypeConfiguration<DeviceAdvancedMotionZoneVertex>
    {
        public void Configure(EntityTypeBuilder<DeviceAdvancedMotionZoneVertex> builder)
        {
            _ = builder.HasKey(v => v.Id);
            _ = builder.HasIndex(v => v.DeviceAdvancedMotionZoneId);
        }
    }

    /// <summary>Fluent API configuration for DeviceZone and its detection-type and vertex child rows. Rows are replaced per settings write.</summary>
    public class DeviceZoneConfiguration : IEntityTypeConfiguration<DeviceZone>
    {
        public void Configure(EntityTypeBuilder<DeviceZone> builder)
        {
            _ = builder.HasKey(z => z.Id);
            _ = builder.Property(z => z.Source).IsRequired().HasMaxLength(16);
            _ = builder.Property(z => z.ZoneId).HasMaxLength(64);
            _ = builder.Property(z => z.Name).HasMaxLength(256);
            _ = builder.Property(z => z.Style).HasMaxLength(64);
            _ = builder.HasIndex(z => z.DeviceId);
            _ = builder.HasMany(z => z.DetectionTypes).WithOne().HasForeignKey(t => t.DeviceZoneId).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasMany(z => z.Vertices).WithOne().HasForeignKey(v => v.DeviceZoneId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for DeviceZoneDetectionType.</summary>
    public class DeviceZoneDetectionTypeConfiguration : IEntityTypeConfiguration<DeviceZoneDetectionType>
    {
        public void Configure(EntityTypeBuilder<DeviceZoneDetectionType> builder)
        {
            _ = builder.HasKey(t => t.Id);
            _ = builder.Property(t => t.DetectionType).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(t => t.DeviceZoneId);
        }
    }

    /// <summary>Fluent API configuration for DeviceZoneVertex.</summary>
    public class DeviceZoneVertexConfiguration : IEntityTypeConfiguration<DeviceZoneVertex>
    {
        public void Configure(EntityTypeBuilder<DeviceZoneVertex> builder)
        {
            _ = builder.HasKey(v => v.Id);
            _ = builder.HasIndex(v => v.DeviceZoneId);
        }
    }

    /// <summary>Fluent API configuration for DeviceAdvancedObjectSetting. Rows are keyed by device and replaced per sync.</summary>
    public class DeviceAdvancedObjectSettingConfiguration : IEntityTypeConfiguration<DeviceAdvancedObjectSetting>
    {
        public void Configure(EntityTypeBuilder<DeviceAdvancedObjectSetting> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.SettingKey).IsRequired().HasMaxLength(64);
            _ = builder.HasIndex(s => s.DeviceId);
        }
    }

    /// <summary>Fluent API configuration for DeviceHealthRpcCommand. Child rows of a DeviceHealths row, written with it.</summary>
    public class DeviceHealthRpcCommandConfiguration : IEntityTypeConfiguration<DeviceHealthRpcCommand>
    {
        public void Configure(EntityTypeBuilder<DeviceHealthRpcCommand> builder)
        {
            _ = builder.HasKey(c => c.Id);
            _ = builder.Property(c => c.Command).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(c => c.DeviceHealthId);
        }
    }
}
