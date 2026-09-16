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
            _ = builder.HasKey(pa => pa.Id);

            _ = builder.Property(pa => pa.ProviderName)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(pa => pa.LastErrorMessage)
                .HasMaxLength(2000);

            _ = builder.HasIndex(pa => new { pa.UserId, pa.ProviderName })
                .IsUnique();

            _ = builder.HasIndex(pa => pa.UserId);
        }
    }
}
