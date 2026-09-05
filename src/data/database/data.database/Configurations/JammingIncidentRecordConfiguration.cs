using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for JammingIncidentRecord entity.</summary>
    public class JammingIncidentRecordConfiguration : IEntityTypeConfiguration<JammingIncidentRecord>
    {
        public void Configure(EntityTypeBuilder<JammingIncidentRecord> builder)
        {
            _ = builder.HasKey(j => j.Id);

            _ = builder.Property(j => j.Notes)
                .HasMaxLength(2000);

            _ = builder.HasIndex(j => j.DeviceId);
        }
    }
}
