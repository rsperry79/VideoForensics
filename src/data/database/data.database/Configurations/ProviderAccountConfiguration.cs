using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ProviderAccount entity.</summary>
    public class ProviderAccountConfiguration : IEntityTypeConfiguration<ProviderAccount>
    {
        public void Configure(EntityTypeBuilder<ProviderAccount> builder)
        {
            builder.HasKey(pa => pa.Id);

            builder.Property(pa => pa.ProviderName)
                .IsRequired()
                .HasMaxLength(256);

            builder.HasIndex(pa => new { pa.UserId, pa.ProviderName })
                .IsUnique();

            builder.HasIndex(pa => pa.UserId);
        }
    }
}
