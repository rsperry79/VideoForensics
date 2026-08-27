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
            builder.HasKey(u => u.Id);

            builder.Property(u => u.ProviderUserKey)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(u => u.Email)
                .HasMaxLength(256);

            builder.HasIndex(u => new { u.ProviderUserKey })
                .IsUnique();
        }
    }
}
