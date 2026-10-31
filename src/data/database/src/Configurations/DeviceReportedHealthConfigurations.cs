using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceReportedHealth. One row per device (unique DeviceId), replaced per sync.</summary>
    public class DeviceReportedHealthConfiguration : IEntityTypeConfiguration<DeviceReportedHealth>
    {
        public void Configure(EntityTypeBuilder<DeviceReportedHealth> builder)
        {
            _ = builder.HasKey(h => h.Id);
            _ = builder.HasIndex(h => h.DeviceId).IsUnique();
        }
    }

    /// <summary>Fluent API configuration for RingDeviceHealthExtras. One row per device (unique DeviceId).</summary>
    public class RingDeviceHealthExtrasConfiguration : IEntityTypeConfiguration<RingDeviceHealthExtras>
    {
        public void Configure(EntityTypeBuilder<RingDeviceHealthExtras> builder)
        {
            _ = builder.HasKey(h => h.Id);
            _ = builder.HasIndex(h => h.DeviceId).IsUnique();
            _ = builder.Property(h => h.DeviceType).HasMaxLength(128);
            _ = builder.Property(h => h.FirmwareVersion).HasMaxLength(128);
            _ = builder.Property(h => h.FirmwareVersionStatus).HasMaxLength(128);
            _ = builder.Property(h => h.NetworkConnectionValue).HasMaxLength(64);
            _ = builder.Property(h => h.OtaStatus).HasMaxLength(64);
            _ = builder.Property(h => h.RssiCategory).HasMaxLength(64);
            _ = builder.Property(h => h.BatteryPercentageCategory).HasMaxLength(64);
            _ = builder.Property(h => h.BatteryVoltageCategory).HasMaxLength(64);
            _ = builder.Property(h => h.SecondBatteryPercentageCategory).HasMaxLength(64);
            _ = builder.Property(h => h.SecondBatteryVoltageCategory).HasMaxLength(64);
            _ = builder.Property(h => h.TransformerVoltageCategory).HasMaxLength(64);
            _ = builder.Property(h => h.TransformerScore).HasMaxLength(64);
            _ = builder.Property(h => h.PacketLossCategory).HasMaxLength(64);
            _ = builder.Property(h => h.WifiName).HasMaxLength(256);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceDetails. One row per device (unique DeviceId).</summary>
    public class RingDeviceDetailsConfiguration : IEntityTypeConfiguration<RingDeviceDetails>
    {
        public void Configure(EntityTypeBuilder<RingDeviceDetails> builder)
        {
            _ = builder.HasKey(r => r.Id);
            _ = builder.HasIndex(r => r.DeviceId).IsUnique();
            _ = builder.Property(r => r.SchemaId).HasMaxLength(256);
            _ = builder.Property(r => r.DeviceResourceId).HasMaxLength(256);
            _ = builder.Property(r => r.RingNetId).HasMaxLength(256);
            _ = builder.Property(r => r.ActiveScheduleUuid).HasMaxLength(256);
            _ = builder.Property(r => r.FlickElimRecommendedMode).HasMaxLength(256);
            _ = builder.Property(r => r.EncryptionGroupId).HasMaxLength(256);
            _ = builder.Property(r => r.AccountGroupRevisionId).HasMaxLength(256);
        }
    }

    /// <summary>Fluent API configuration for DeviceReportedBattery. One row per battery per device, replaced per sync.</summary>
    public class DeviceReportedBatteryConfiguration : IEntityTypeConfiguration<DeviceReportedBattery>
    {
        public void Configure(EntityTypeBuilder<DeviceReportedBattery> builder)
        {
            _ = builder.HasKey(b => b.Id);
            _ = builder.Property(b => b.BatteryId).HasMaxLength(128);
            _ = builder.Property(b => b.PercentageCategory).HasMaxLength(64);
            _ = builder.Property(b => b.VoltageCategory).HasMaxLength(64);
            _ = builder.Property(b => b.AuthStatus).HasMaxLength(64);
            _ = builder.Property(b => b.Family).HasMaxLength(128);
            _ = builder.Property(b => b.FwVersion).HasMaxLength(128);
            _ = builder.Property(b => b.OtaStatus).HasMaxLength(64);
            _ = builder.HasIndex(b => new { b.DeviceId, b.Position }).IsUnique();
        }
    }
}
