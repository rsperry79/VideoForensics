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
            builder.HasKey(dcs => dcs.Id);

            builder.Property(dcs => dcs.MotionSensitivity)
                .HasMaxLength(256);

            builder.Property(dcs => dcs.RecordingMode)
                .HasMaxLength(256);

            builder.HasIndex(dcs => dcs.DeviceId);
        }
    }
}
