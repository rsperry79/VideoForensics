using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for LegalHold entity.</summary>
    public class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
    {
        public void Configure(EntityTypeBuilder<LegalHold> builder)
        {
            _ = builder.HasKey(h => h.Id);

            _ = builder.Property(h => h.Reason)
                .IsRequired()
                .HasMaxLength(1024);

            _ = builder.Property(h => h.CreatedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(h => h.ReleasedBy)
                .HasMaxLength(256);

            _ = builder.HasIndex(h => h.MediaItemId);
        }
    }
}
