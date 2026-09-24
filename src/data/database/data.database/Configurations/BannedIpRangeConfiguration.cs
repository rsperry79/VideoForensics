using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for BannedIpRange entity.</summary>
    public class BannedIpRangeConfiguration : IEntityTypeConfiguration<BannedIpRange>
    {
        public void Configure(EntityTypeBuilder<BannedIpRange> builder)
        {
            _ = builder.HasKey(b => b.Id);
            _ = builder.Property(b => b.CidrRange).IsRequired().HasMaxLength(64);
            _ = builder.Property(b => b.Reason).HasMaxLength(512);
            _ = builder.Property(b => b.CreatedAtUtc).IsRequired();
            _ = builder.Property(b => b.CreatedByOperatorId).IsRequired();

            // Index on CidrRange for lookup performance during login checks
            _ = builder.HasIndex(b => b.CidrRange);
        }
    }
}
