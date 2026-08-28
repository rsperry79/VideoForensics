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
            builder.HasKey(dh => dh.Id);

            builder.Property(dh => dh.WifiName)
                .HasMaxLength(256);

            builder.Property(dh => dh.Status)
                .HasMaxLength(256);

            builder.Property(dh => dh.SyncStatus)
                .HasDefaultValue(0);

            builder.Property(dh => dh.ApiResponseHash)
                .HasMaxLength(256);

            builder.HasIndex(dh => dh.DeviceId)
                .IsUnique();
        }
    }
}
