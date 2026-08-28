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
            builder.HasKey(ra => ra.Id);

            builder.Property(ra => ra.SubscriptionLevel)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(ra => ra.Features)
                .HasMaxLength(2000);

            builder.Property(ra => ra.AccountEmail)
                .HasMaxLength(256);

            builder.Property(ra => ra.ApiResponseHash)
                .HasMaxLength(256);

            builder.HasIndex(ra => ra.ProviderAccountId)
                .IsUnique();
        }
    }
}
