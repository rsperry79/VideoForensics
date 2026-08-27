using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ProviderReconciliationRecord entity.</summary>
    public class ProviderReconciliationRecordConfiguration : IEntityTypeConfiguration<ProviderReconciliationRecord>
    {
        public void Configure(EntityTypeBuilder<ProviderReconciliationRecord> builder)
        {
            builder.HasKey(prr => prr.Id);

            builder.Property(prr => prr.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(prr => prr.FieldName)
                .HasMaxLength(256);

            builder.Property(prr => prr.StoredValue)
                .HasMaxLength(2048);

            builder.Property(prr => prr.ProviderValue)
                .HasMaxLength(2048);

            builder.Property(prr => prr.Notes)
                .HasMaxLength(512);

            builder.HasIndex(prr => prr.DeviceId);
        }
    }
}
