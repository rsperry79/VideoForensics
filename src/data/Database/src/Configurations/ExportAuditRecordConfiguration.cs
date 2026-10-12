using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ExportAuditRecordEntity.</summary>
    public class ExportAuditRecordConfiguration : AuditConfigurationBase, IEntityTypeConfiguration<ExportAuditRecordEntity>
    {
        public void Configure(EntityTypeBuilder<ExportAuditRecordEntity> builder)
        {
            _ = builder.HasKey(ear => ear.Id);

            _ = builder.Property(ear => ear.ExportedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            _ = builder.Property(ear => ear.ExportFormat)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            _ = builder.Property(ear => ear.Purpose)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            _ = builder.HasIndex(ear => ear.LocationId);
            _ = builder.HasIndex(ear => ear.ExportedAtUtc);
        }
    }
}
