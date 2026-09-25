using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for LockoutPolicySettings entity (singleton settings).</summary>
    public class LockoutPolicySettingsConfiguration : IEntityTypeConfiguration<LockoutPolicySettings>
    {
        public void Configure(EntityTypeBuilder<LockoutPolicySettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.MaxFailedAttempts).IsRequired();
            _ = builder.Property(s => s.LockoutDurationMinutes).IsRequired();
            _ = builder.Property(s => s.BlockedCountryCodes).HasMaxLength(256);
            _ = builder.Property(s => s.FailClosedOnLookupError).IsRequired();
            _ = builder.Property(s => s.UpdatedAtUtc).IsRequired();
        }
    }
}
