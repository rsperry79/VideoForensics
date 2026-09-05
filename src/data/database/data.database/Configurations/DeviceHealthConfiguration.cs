using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DeviceHealth entity.</summary>
    public class DeviceHealthConfiguration : IEntityTypeConfiguration<DeviceHealth>
    {
        public void Configure(EntityTypeBuilder<DeviceHealth> builder)
        {
            _ = builder.HasKey(dh => dh.Id);

            _ = builder.Property(dh => dh.WifiName)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.Status)
                .HasMaxLength(256);

            _ = builder.Property(dh => dh.ApiResponseHash)
                .HasMaxLength(256);

            _ = builder.HasIndex(dh => dh.DeviceId)
                .IsUnique();
        }
    }
}
