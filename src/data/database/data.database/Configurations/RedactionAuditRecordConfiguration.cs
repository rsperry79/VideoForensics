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
            builder.HasKey(rar => rar.Id);

            builder.Property(rar => rar.RedactedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(rar => rar.ApprovedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(rar => rar.ContentRedacted)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            builder.Property(rar => rar.JustificationNotes)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            builder.HasIndex(rar => rar.EvidenceId);
            builder.HasIndex(rar => rar.RedactedAtUtc);
        }
    }
}
