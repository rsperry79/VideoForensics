using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Credential entity.</summary>
    public class CredentialConfiguration : IEntityTypeConfiguration<Credential>
    {
        public void Configure(EntityTypeBuilder<Credential> builder)
        {
            _ = builder.HasKey(c => c.Id);

            _ = builder.Property(c => c.CredentialType)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(c => c.EncryptedValue)
                .IsRequired();

            _ = builder.Property(c => c.EncryptionProvider)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.HasIndex(c => new { c.ProviderAccountId, c.CredentialType })
                .IsUnique();

            _ = builder.HasIndex(c => c.ProviderAccountId);
        }
    }
}
