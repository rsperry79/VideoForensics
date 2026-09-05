using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for IntegrityRecord entity.</summary>
    public class IntegrityRecordConfiguration : IEntityTypeConfiguration<IntegrityRecord>
    {
        public void Configure(EntityTypeBuilder<IntegrityRecord> builder)
        {
            _ = builder.HasKey(ir => ir.Id);

            _ = builder.Property(ir => ir.Sha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(ir => ir.FailureReason)
                .HasMaxLength(256);

            _ = builder.Property(ir => ir.VerifiedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(ir => ir.MediaItemId);
        }
    }
}
