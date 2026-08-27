using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for JammingStatsSummary entity.</summary>
    public class JammingStatsSummaryConfiguration : IEntityTypeConfiguration<JammingStatsSummary>
    {
        public void Configure(EntityTypeBuilder<JammingStatsSummary> builder)
        {
            builder.HasKey(j => j.Id);

            builder.HasIndex(j => j.DeviceId)
                .IsUnique();
        }
    }
}
