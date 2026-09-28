using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Alert entity.</summary>
    public class AlertConfiguration : IEntityTypeConfiguration<Alert>
    {
        public void Configure(EntityTypeBuilder<Alert> builder)
        {
            _ = builder.HasKey(a => a.Id);

            _ = builder.Property(a => a.Title)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.Description)
                .HasMaxLength(4000);

            _ = builder.Property(a => a.Status)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(a => a.CreatedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.AlertType)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.HasIndex(a => a.RelatedCaseId);
            _ = builder.HasIndex(a => a.Status);
            _ = builder.HasIndex(a => a.AlertType);
            _ = builder.HasIndex(a => a.CreatedAtUtc);
        }
    }
}
