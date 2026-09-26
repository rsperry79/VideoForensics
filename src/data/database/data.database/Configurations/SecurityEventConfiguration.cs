using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for SecurityEvent entity.</summary>
    public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
    {
        public void Configure(EntityTypeBuilder<SecurityEvent> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.OperatorId).IsRequired();
            _ = builder.Property(e => e.EventType).IsRequired();
            _ = builder.Property(e => e.Success).IsRequired();
            _ = builder.Property(e => e.IpAddress).HasMaxLength(64);
            _ = builder.Property(e => e.OccurredAtUtc).IsRequired();
            _ = builder.Property(e => e.Reason).HasMaxLength(512);
            _ = builder.Property(e => e.CreatedAtUtc).IsRequired();

            // Default EF Core naming convention already produces
            // IX_SecurityEvents_OperatorId_OccurredAtUtc and IX_SecurityEvents_OccurredAtUtc,
            // matching the migration.
            _ = builder.HasIndex(e => new { e.OperatorId, e.OccurredAtUtc });
            _ = builder.HasIndex(e => e.OccurredAtUtc);
        }
    }
}
