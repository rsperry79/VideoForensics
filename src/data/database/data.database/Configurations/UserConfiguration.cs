using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for User entity.</summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            _ = builder.HasKey(u => u.Id);

            _ = builder.Property(u => u.ProviderUserKey)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(u => u.Email)
                .HasMaxLength(256);

            _ = builder.HasIndex(u => new { u.ProviderUserKey })
                .IsUnique();
        }
    }
}
