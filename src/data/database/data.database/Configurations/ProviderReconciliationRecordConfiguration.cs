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
            _ = builder.HasKey(prr => prr.Id);

            _ = builder.Property(prr => prr.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(prr => prr.FieldName)
                .HasMaxLength(256);

            _ = builder.Property(prr => prr.StoredValue)
                .HasMaxLength(2048);

            _ = builder.Property(prr => prr.ProviderValue)
                .HasMaxLength(2048);

            _ = builder.Property(prr => prr.Notes)
                .HasMaxLength(512);

            _ = builder.HasIndex(prr => prr.DeviceId);
        }
    }
}
