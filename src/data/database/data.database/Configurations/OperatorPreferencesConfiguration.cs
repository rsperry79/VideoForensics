using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for OperatorPreferences entity.</summary>
    public class OperatorPreferencesConfiguration : IEntityTypeConfiguration<OperatorPreferences>
    {
        public void Configure(EntityTypeBuilder<OperatorPreferences> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.Property(p => p.ThemeMode).IsRequired().HasMaxLength(16);
            _ = builder.Property(p => p.CultureName).HasMaxLength(16);

            _ = builder.HasIndex(p => p.OperatorId).IsUnique();
        }
    }
}
