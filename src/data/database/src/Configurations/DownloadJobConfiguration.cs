using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DownloadJob entity.</summary>
    public class DownloadJobConfiguration : IEntityTypeConfiguration<DownloadJob>
    {
        public void Configure(EntityTypeBuilder<DownloadJob> builder)
        {
            _ = builder.ToTable("DownloadJobs");

            _ = builder.HasKey(j => j.Id);

            _ = builder.Property(j => j.Kind)
                .HasConversion<string>()
                .HasMaxLength(16);

            _ = builder.Property(j => j.State)
                .HasConversion<string>()
                .HasMaxLength(16);

            _ = builder.Property(j => j.ErrorMessage)
                .HasMaxLength(1024);

            // The runner claims by state then age; the account page lists by account then age.
            _ = builder.HasIndex(j => new { j.State, j.CreatedUtc });
            _ = builder.HasIndex(j => new { j.ProviderAccountId, j.CreatedUtc });
        }
    }
}
