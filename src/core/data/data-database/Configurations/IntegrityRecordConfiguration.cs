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
            builder.HasKey(ir => ir.Id);

            builder.Property(ir => ir.Sha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(ir => ir.FailureReason)
                .HasMaxLength(256);

            builder.Property(ir => ir.VerifiedBy)
                .IsRequired()
                .HasMaxLength(256);

            builder.HasIndex(ir => ir.MediaItemId);
        }
    }
}
