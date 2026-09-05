using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RedactionAuditRecordEntity.</summary>
    public class RedactionAuditRecordConfiguration : AuditConfigurationBase, IEntityTypeConfiguration<RedactionAuditRecordEntity>
    {
        public void Configure(EntityTypeBuilder<RedactionAuditRecordEntity> builder)
        {
            _ = builder.HasKey(rar => rar.Id);

            _ = builder.Property(rar => rar.RedactedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(rar => rar.ApprovedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(rar => rar.ContentRedacted)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            _ = builder.Property(rar => rar.JustificationNotes)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            _ = builder.HasIndex(rar => rar.EvidenceId);
            _ = builder.HasIndex(rar => rar.RedactedAtUtc);
        }
    }
}
