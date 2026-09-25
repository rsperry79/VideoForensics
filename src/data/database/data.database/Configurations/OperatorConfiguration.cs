using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Operator entity.</summary>
    public class OperatorConfiguration : IEntityTypeConfiguration<Operator>
    {
        public void Configure(EntityTypeBuilder<Operator> builder)
        {
            _ = builder.HasKey(o => o.Id);
            _ = builder.Property(o => o.DisplayName).IsRequired().HasMaxLength(256);
            _ = builder.Property(o => o.Username).IsRequired().HasMaxLength(256);
            _ = builder.Property(o => o.Email).IsRequired().HasMaxLength(256);
            _ = builder.Property(o => o.FirstName).HasMaxLength(256);
            _ = builder.Property(o => o.LastName).HasMaxLength(256);
            _ = builder.Property(o => o.Phone).HasMaxLength(64);
            _ = builder.Property(o => o.PasswordHash).HasMaxLength(512);
            _ = builder.Property(o => o.IsPrimarySuperAdmin).IsRequired();
            _ = builder.Property(o => o.FailedLoginAttemptCount).IsRequired();
            _ = builder.Property(o => o.TwoFactorRequirementOverride).IsRequired();

            _ = builder.HasIndex(o => o.Username).IsUnique();
            _ = builder.HasIndex(o => o.Email).IsUnique();
        }
    }
}
