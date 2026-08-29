using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ModificationAuditRecordEntity.</summary>
    public class ModificationAuditRecordConfiguration : IEntityTypeConfiguration<ModificationAuditRecordEntity>
    {
        public void Configure(EntityTypeBuilder<ModificationAuditRecordEntity> builder)
        {
            builder.HasKey(mar => mar.Id);

            builder.Property(mar => mar.ModifiedBy)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(mar => mar.ModificationType)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(mar => mar.ChangeSummary)
                .IsRequired()
                .HasMaxLength(2000);

            builder.HasIndex(mar => mar.EventId);
            builder.HasIndex(mar => mar.ModifiedAtUtc);
        }
    }
}
