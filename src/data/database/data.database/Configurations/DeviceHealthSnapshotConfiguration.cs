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
            builder.HasKey(dh => dh.Id);

            builder.Property(dh => dh.WifiName)
                .HasMaxLength(256);

            builder.Property(dh => dh.FirmwareVersion)
                .HasMaxLength(256);

            builder.HasIndex(dh => dh.DownloadEventId);
            builder.HasIndex(dh => new { dh.DeviceId, dh.CapturedAtUtc });
        }
    }
}
