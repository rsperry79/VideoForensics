using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingAccount entity.</summary>
    public class RingAccountConfiguration : IEntityTypeConfiguration<RingAccount>
    {
        public void Configure(EntityTypeBuilder<RingAccount> builder)
        {
            _ = builder.HasKey(ra => ra.Id);

            _ = builder.Property(ra => ra.SubscriptionLevel)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(ra => ra.Features)
                .HasMaxLength(2000);

            _ = builder.Property(ra => ra.AccountEmail)
                .HasMaxLength(256);

            _ = builder.Property(ra => ra.ApiResponseHash)
                .HasMaxLength(256);

            _ = builder.HasIndex(ra => ra.ProviderAccountId)
                .IsUnique();
        }
    }
}
