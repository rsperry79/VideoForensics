using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceCapabilities entity.</summary>
    public class DeviceCapabilitiesConfiguration : IEntityTypeConfiguration<DeviceCapabilities>
    {
        public void Configure(EntityTypeBuilder<DeviceCapabilities> builder)
        {
            builder.HasKey(dc => dc.Id);

            builder.Property(dc => dc.Resolution)
                .HasMaxLength(256);

            builder.Property(dc => dc.StorageType)
                .HasMaxLength(256);

            builder.Property(dc => dc.FirmwareVersion)
                .HasMaxLength(256);

            builder.Property(dc => dc.HardwareModel)
                .HasMaxLength(256);

            builder.Property(dc => dc.ApiResponseHash)
                .HasMaxLength(256);

            builder.HasIndex(dc => dc.DeviceId)
                .IsUnique();
        }
    }
}
