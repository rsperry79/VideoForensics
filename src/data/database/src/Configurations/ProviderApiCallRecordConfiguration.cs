using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ProviderApiCallRecord entity.</summary>
    public class ProviderApiCallRecordConfiguration : IEntityTypeConfiguration<ProviderApiCallRecord>
    {
        public void Configure(EntityTypeBuilder<ProviderApiCallRecord> builder)
        {
            _ = builder.HasKey(r => r.Id);
            _ = builder.Property(r => r.ProviderName).IsRequired().HasMaxLength(64);
            _ = builder.HasIndex(r => new { r.ProviderName, r.TimestampUtc });
        }
    }
}
