using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for SecurityAlert entity.</summary>
    public class SecurityAlertConfiguration : IEntityTypeConfiguration<SecurityAlert>
    {
        public void Configure(EntityTypeBuilder<SecurityAlert> builder)
        {
            _ = builder.HasKey(s => s.Id);

            _ = builder.Property(s => s.Severity)
                .HasMaxLength(64);

            _ = builder.Property(s => s.AlertText)
                .HasMaxLength(512);

            _ = builder.HasIndex(s => s.MediaItemId);
        }
    }
}
