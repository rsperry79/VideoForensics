using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for LocationMetadata entity.</summary>
    public class LocationMetadataConfiguration : IEntityTypeConfiguration<LocationMetadata>
    {
        public void Configure(EntityTypeBuilder<LocationMetadata> builder)
        {
            _ = builder.HasKey(lm => lm.Id);

            _ = builder.Property(lm => lm.StreetAddress)
                .HasMaxLength(512);

            _ = builder.Property(lm => lm.City)
                .HasMaxLength(256);

            _ = builder.Property(lm => lm.State)
                .HasMaxLength(256);

            _ = builder.Property(lm => lm.PostalCode)
                .HasMaxLength(256);

            _ = builder.Property(lm => lm.Country)
                .HasMaxLength(256);

            _ = builder.Property(lm => lm.TimeZoneId)
                .HasMaxLength(256);

            _ = builder.Property(lm => lm.ApiResponseHash)
                .HasMaxLength(256);

            _ = builder.HasIndex(lm => lm.LocationId)
                .IsUnique();
        }
    }
}
