using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingAccountFeatures entity.</summary>
    public class RingAccountFeaturesConfiguration : IEntityTypeConfiguration<RingAccountFeatures>
    {
        public void Configure(EntityTypeBuilder<RingAccountFeatures> builder)
        {
            _ = builder.HasKey(raf => raf.Id);

            _ = builder.Property(raf => raf.Email)
                .HasMaxLength(256);

            _ = builder.Property(raf => raf.FirstName)
                .HasMaxLength(256);

            _ = builder.Property(raf => raf.LastName)
                .HasMaxLength(256);

            _ = builder.Property(raf => raf.PhoneNumber)
                .HasMaxLength(256);

            _ = builder.HasIndex(raf => raf.RingAccountId);
        }
    }
}
