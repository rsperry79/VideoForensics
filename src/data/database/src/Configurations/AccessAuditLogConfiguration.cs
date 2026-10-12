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
            _ = builder.HasKey(aal => aal.Id);

            _ = builder.Property(aal => aal.UserId)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(aal => aal.Action)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            _ = builder.Property(aal => aal.IpAddress)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(aal => aal.Purpose)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            _ = builder.HasIndex(aal => aal.EvidenceId);
            _ = builder.HasIndex(aal => aal.UserId);
            _ = builder.HasIndex(aal => aal.AccessedAtUtc);
        }
    }
}
