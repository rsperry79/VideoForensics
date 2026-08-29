using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ModificationAuditRecordEntity.</summary>
    public class ModificationAuditRecordConfiguration : AuditConfigurationBase, IEntityTypeConfiguration<ModificationAuditRecordEntity>
    {
        public void Configure(EntityTypeBuilder<ModificationAuditRecordEntity> builder)
        {
            builder.HasKey(mar => mar.Id);

            builder.Property(mar => mar.ModifiedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(mar => mar.ModificationType)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            builder.Property(mar => mar.ChangeSummary)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            builder.HasIndex(mar => mar.EventId);
            builder.HasIndex(mar => mar.ModifiedAtUtc);
        }
    }
}
