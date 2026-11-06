using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingDevicePaidFeatures. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDevicePaidFeaturesConfiguration : IEntityTypeConfiguration<RingDevicePaidFeatures>
    {
        public void Configure(EntityTypeBuilder<RingDevicePaidFeatures> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.HasIndex(p => p.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDevicePaidFeatures>(p => p.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for DeviceNetworkSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class DeviceNetworkSettingsConfiguration : IEntityTypeConfiguration<DeviceNetworkSettings>
    {
        public void Configure(EntityTypeBuilder<DeviceNetworkSettings> builder)
        {
            _ = builder.HasKey(n => n.Id);
            _ = builder.HasIndex(n => n.DeviceId).IsUnique();
            _ = builder.Property(n => n.MacAddressBle).HasMaxLength(64);
            _ = builder.Property(n => n.MacAddressWifi24).HasMaxLength(64);
            _ = builder.Property(n => n.MacAddressWifi5).HasMaxLength(64);
            _ = builder.Property(n => n.MaxDynamicListenInterval).HasMaxLength(256);
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<DeviceNetworkSettings>(n => n.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceNetworkDiagnosis. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceNetworkDiagnosisConfiguration : IEntityTypeConfiguration<RingDeviceNetworkDiagnosis>
    {
        public void Configure(EntityTypeBuilder<RingDeviceNetworkDiagnosis> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.HasIndex(d => d.DeviceId).IsUnique();
            _ = builder.Property(d => d.TcpHost).HasMaxLength(256);
            _ = builder.Property(d => d.UdpHost).HasMaxLength(256);
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceNetworkDiagnosis>(d => d.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
