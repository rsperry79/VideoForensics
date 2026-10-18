using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ProviderSyncRun entity.</summary>
    public class ProviderSyncRunConfiguration : IEntityTypeConfiguration<ProviderSyncRun>
    {
        public void Configure(EntityTypeBuilder<ProviderSyncRun> builder)
        {
            _ = builder.ToTable("ProviderSyncRuns");

            _ = builder.HasKey(r => r.Id);

            _ = builder.Property(r => r.ErrorMessage)
                .HasMaxLength(1024);

            _ = builder.Property(r => r.SyncType)
                .HasMaxLength(16)
                .HasDefaultValue("Unknown");

            // Serves the account details list: filter by account, newest first.
            _ = builder.HasIndex(r => new { r.ProviderAccountId, r.StartedUtc });
        }
    }
}
