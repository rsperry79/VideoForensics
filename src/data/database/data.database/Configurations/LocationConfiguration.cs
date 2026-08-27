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
            builder.HasKey(l => l.Id);

            builder.Property(l => l.ProviderLocationId)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(l => l.Name)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(l => l.Address)
                .HasMaxLength(512);

            builder.HasIndex(l => new { l.ProviderAccountId, l.ProviderLocationId })
                .IsUnique();

            builder.HasIndex(l => l.ProviderAccountId);
        }
    }
}
