using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for TwoFactorRoleRequirement entity.</summary>
    public class TwoFactorRoleRequirementConfiguration : IEntityTypeConfiguration<TwoFactorRoleRequirement>
    {
        public void Configure(EntityTypeBuilder<TwoFactorRoleRequirement> builder)
        {
            _ = builder.HasKey(r => r.Id);
            _ = builder.Property(r => r.Role).IsRequired();
            _ = builder.Property(r => r.RequireTwoFactor).IsRequired();
            _ = builder.Property(r => r.UpdatedAtUtc).IsRequired();

            // Unique index on Role to ensure exactly one requirement row per OperatorRole value
            _ = builder.HasIndex(r => r.Role).IsUnique();
        }
    }
}
