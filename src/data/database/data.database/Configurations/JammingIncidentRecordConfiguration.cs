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
            builder.HasKey(j => j.Id);

            builder.Property(j => j.Notes)
                .HasMaxLength(2000);

            builder.HasIndex(j => j.DeviceId);
        }
    }
}
