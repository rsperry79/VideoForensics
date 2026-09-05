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
            _ = builder.HasKey(mar => mar.Id);

            _ = builder.Property(mar => mar.ModifiedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(mar => mar.ModificationType)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            _ = builder.Property(mar => mar.ChangeSummary)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            _ = builder.HasIndex(mar => mar.EventId);
            _ = builder.HasIndex(mar => mar.ModifiedAtUtc);
        }
    }
}
