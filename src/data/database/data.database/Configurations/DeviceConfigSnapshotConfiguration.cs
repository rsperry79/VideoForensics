using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceConfigSnapshot entity.</summary>
    public class DeviceConfigSnapshotConfiguration : IEntityTypeConfiguration<DeviceConfigSnapshot>
    {
        public void Configure(EntityTypeBuilder<DeviceConfigSnapshot> builder)
        {
            _ = builder.HasKey(dcs => dcs.Id);

            _ = builder.Property(dcs => dcs.MotionSensitivity)
                .HasMaxLength(256);

            _ = builder.Property(dcs => dcs.RecordingMode)
                .HasMaxLength(256);

            _ = builder.HasIndex(dcs => dcs.DeviceId);
        }
    }
}
