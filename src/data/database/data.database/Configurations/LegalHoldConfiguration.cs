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
            builder.HasKey(h => h.Id);

            builder.Property(h => h.Reason)
                .IsRequired()
                .HasMaxLength(1024);

            builder.Property(h => h.CreatedBy)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(h => h.ReleasedBy)
                .HasMaxLength(256);

            builder.HasIndex(h => h.MediaItemId);
        }
    }
}
