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
            builder.HasKey(e => e.Id);
            builder.Property(e => e.EventType).IsRequired().HasMaxLength(64);
            builder.Property(e => e.SourceIp).HasMaxLength(64);
            builder.Property(e => e.Details).HasMaxLength(2048);

            builder.HasIndex(e => e.TimestampUtc);
            builder.HasIndex(e => e.OperatorId);
        }
    }
}
