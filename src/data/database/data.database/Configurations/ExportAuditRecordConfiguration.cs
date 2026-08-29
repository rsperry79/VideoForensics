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
            builder.HasKey(ear => ear.Id);

            builder.Property(ear => ear.ExportedBy)
                .IsRequired()
                .HasMaxLength(ActorMaxLength);

            builder.Property(ear => ear.ExportFormat)
                .IsRequired()
                .HasMaxLength(ActionMaxLength);

            builder.Property(ear => ear.Purpose)
                .IsRequired()
                .HasMaxLength(DescriptionMaxLength);

            builder.HasIndex(ear => ear.LocationId);
            builder.HasIndex(ear => ear.ExportedAtUtc);
        }
    }
}
