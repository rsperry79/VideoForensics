using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceLocation entity.</summary>
    public class DeviceLocationConfiguration : IEntityTypeConfiguration<DeviceLocation>
    {
        public void Configure(EntityTypeBuilder<DeviceLocation> builder)
        {
            _ = builder.HasKey(dl => dl.Id);

            _ = builder.Property(dl => dl.Address)
                .HasMaxLength(512);

            _ = builder.HasIndex(dl => dl.DeviceId);
        }
    }
}
