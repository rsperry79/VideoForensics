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
            builder.HasKey(d => d.Id);

            builder.Property(d => d.ProviderDeviceId)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(d => d.Type)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(d => d.TimeZoneId)
                .HasMaxLength(256);

            builder.HasIndex(d => new { d.LocationId, d.ProviderDeviceId })
                .IsUnique();

            builder.HasIndex(d => d.LocationId);
        }
    }
}
