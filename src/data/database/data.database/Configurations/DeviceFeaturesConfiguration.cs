using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceFeatures entity.</summary>
    public class DeviceFeaturesConfiguration : IEntityTypeConfiguration<DeviceFeatures>
    {
        public void Configure(EntityTypeBuilder<DeviceFeatures> builder)
        {
            _ = builder.HasKey(df => df.Id);

            _ = builder.HasIndex(df => df.DeviceId);
        }
    }
}
