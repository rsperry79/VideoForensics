using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceHealthSnapshot entity.</summary>
    public class DeviceHealthSnapshotConfiguration : IEntityTypeConfiguration<DeviceHealthSnapshot>
    {
        public void Configure(EntityTypeBuilder<DeviceHealthSnapshot> builder)
        {
            _ = builder.HasKey(dh => dh.Id);

            _ = builder.Property(dh => dh.WifiName)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.FirmwareVersion)
                .HasMaxLength(256);

            _ = builder.HasIndex(dh => dh.DownloadEventId);
            _ = builder.HasIndex(dh => new { dh.DeviceId, dh.CapturedAtUtc });
        }
    }
}
