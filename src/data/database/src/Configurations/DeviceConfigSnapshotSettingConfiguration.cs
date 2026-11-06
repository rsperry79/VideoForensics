using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceConfigSnapshotSetting entity.</summary>
    public class DeviceConfigSnapshotSettingConfiguration : IEntityTypeConfiguration<DeviceConfigSnapshotSetting>
    {
        public void Configure(EntityTypeBuilder<DeviceConfigSnapshotSetting> builder)
        {
            _ = builder.HasKey(s => s.Id);

            _ = builder.Property(s => s.Key)
                .HasMaxLength(256)
                .IsRequired();

            _ = builder.Property(s => s.ValueJson)
                .IsRequired();

            _ = builder.HasIndex(s => new { s.SnapshotId, s.Key })
                .IsUnique();
        }
    }
}
