using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceSettingChange entity.</summary>
    public class DeviceSettingChangeConfiguration : IEntityTypeConfiguration<DeviceSettingChange>
    {
        public void Configure(EntityTypeBuilder<DeviceSettingChange> builder)
        {
            _ = builder.HasKey(dsc => dsc.Id);

            _ = builder.Property(dsc => dsc.FieldName)
                .IsRequired()
                .HasMaxLength(64);

            // Per-device history lookups are ordered by time, so the composite index serves both filter and sort.
            _ = builder.HasIndex(dsc => new { dsc.DeviceId, dsc.ChangedAtUtc });
        }
    }
}
