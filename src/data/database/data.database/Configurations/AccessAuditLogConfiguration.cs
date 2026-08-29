using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for AccessAuditLogEntity.</summary>
    public class AccessAuditLogConfiguration : AuditConfigurationBase, IEntityTypeConfiguration<AccessAuditLogEntity>
    {
        public void Configure(EntityTypeBuilder<AccessAuditLogEntity> builder)
        {
            builder.HasKey(aal => aal.Id);

            builder.Property(aal => aal.UserId)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(aal => aal.Action)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            builder.Property(aal => aal.IpAddress)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(aal => aal.Purpose)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            builder.HasIndex(aal => aal.EvidenceId);
            builder.HasIndex(aal => aal.UserId);
            builder.HasIndex(aal => aal.AccessedAtUtc);
        }
    }
}
