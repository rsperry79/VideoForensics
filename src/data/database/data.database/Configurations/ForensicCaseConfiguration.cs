using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ForensicCase entity.</summary>
    public class ForensicCaseConfiguration : IEntityTypeConfiguration<ForensicCase>
    {
        public void Configure(EntityTypeBuilder<ForensicCase> builder)
        {
            _ = builder.HasKey(c => c.Id);

            _ = builder.Property(c => c.CaseNumber)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.HasIndex(c => c.CaseNumber)
                .IsUnique();

            _ = builder.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(c => c.Description)
                .HasMaxLength(4000);

            _ = builder.Property(c => c.Status)
                .IsRequired()
                .HasConversion<string>();

            _ = builder.Property(c => c.CreatedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(c => c.ClosedBy)
                .HasMaxLength(256);
        }
    }
}
