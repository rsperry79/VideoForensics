using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for SecurityAuditLogEntry entity.</summary>
    public class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLogEntry>
    {
        public void Configure(EntityTypeBuilder<SecurityAuditLogEntry> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.EventType).IsRequired().HasMaxLength(64);
            _ = builder.Property(e => e.SourceIp).HasMaxLength(64);
            _ = builder.Property(e => e.Details).HasMaxLength(2048);

            _ = builder.HasIndex(e => e.TimestampUtc);
            _ = builder.HasIndex(e => e.OperatorId);
        }
    }
}
