using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceAlerts entity.</summary>
    public class DeviceAlertsConfiguration : IEntityTypeConfiguration<DeviceAlerts>
    {
        public void Configure(EntityTypeBuilder<DeviceAlerts> builder)
        {
            _ = builder.HasKey(da => da.Id);

            _ = builder.Property(da => da.AlertType)
                .HasMaxLength(256);

            _ = builder.HasIndex(da => new { da.DeviceId, da.AlertType });
        }
    }
}
