using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for EventSecurityAlert entity.</summary>
    public class EventSecurityAlertConfiguration : IEntityTypeConfiguration<EventSecurityAlert>
    {
        public void Configure(EntityTypeBuilder<EventSecurityAlert> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Severity)
                .HasMaxLength(64);

            _ = builder.Property(e => e.AlertText)
                .HasMaxLength(512);

            _ = builder.HasIndex(e => e.EventId);
        }
    }
}
