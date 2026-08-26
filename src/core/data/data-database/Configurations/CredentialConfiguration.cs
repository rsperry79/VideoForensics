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
            builder.HasKey(c => c.Id);

            builder.Property(c => c.CredentialType)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(c => c.EncryptedValue)
                .IsRequired();

            builder.Property(c => c.EncryptionProvider)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(c => new { c.ProviderAccountId, c.CredentialType })
                .IsUnique();

            builder.HasIndex(c => c.ProviderAccountId);
        }
    }
}
