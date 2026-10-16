using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceHealth entity.</summary>
    public class DeviceHealthConfiguration : IEntityTypeConfiguration<DeviceHealth>
    {
        public void Configure(EntityTypeBuilder<DeviceHealth> builder)
        {
            _ = builder.HasKey(dh => dh.Id);

            _ = builder.Property(dh => dh.WifiName)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.OtaStatus)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.FirmwareVersion)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.DeviceType)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.FirmwareVersionStatus)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.BatteryPercentageCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.BatteryVoltageCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.RssiCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.NetworkConnectionValue)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.PacketLossCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.LatestSignalCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.AverageSignalCategory)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.Firmware)
                .HasMaxLength(256);

            _ = builder.HasIndex(dh => new { dh.DeviceId, dh.CapturedAtUtc });
        }
    }
}
