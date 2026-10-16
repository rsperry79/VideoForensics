using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Device entity.</summary>
    public class DeviceConfiguration : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            _ = builder.HasKey(d => d.Id);

            _ = builder.Property(d => d.ProviderDeviceId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(d => d.Type)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(d => d.TimeZoneId)
                .HasMaxLength(256);

            _ = builder.Property(d => d.RingDeviceId)
                .HasMaxLength(256);

            _ = builder.Property(d => d.RingKind)
                .HasMaxLength(256);

            _ = builder.Property(d => d.FirmwareVersion)
                .HasMaxLength(256);

            _ = builder.Property(d => d.Address)
                .HasMaxLength(512);

            _ = builder.Property(d => d.BatteryLife)
                .HasMaxLength(256);

            _ = builder.HasIndex(d => new { d.LocationId, d.ProviderDeviceId })
                .IsUnique();

            _ = builder.HasIndex(d => d.LocationId);
        }
    }
}
