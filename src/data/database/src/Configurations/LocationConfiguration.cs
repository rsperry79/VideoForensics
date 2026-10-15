using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Location entity.</summary>
    public class LocationConfiguration : IEntityTypeConfiguration<Location>
    {
        public void Configure(EntityTypeBuilder<Location> builder)
        {
            _ = builder.HasKey(l => l.Id);

            _ = builder.Property(l => l.ProviderLocationId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(l => l.Name)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(l => l.Address)
                .HasMaxLength(512);

            // ProviderLocationId is now globally unique (no longer scoped by ProviderAccountId)
            _ = builder.HasIndex(l => l.ProviderLocationId)
                .IsUnique();
        }
    }
}
