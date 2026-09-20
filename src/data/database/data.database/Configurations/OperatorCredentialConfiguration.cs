using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for OperatorCredential entity.</summary>
    public class OperatorCredentialConfiguration : IEntityTypeConfiguration<OperatorCredential>
    {
        public void Configure(EntityTypeBuilder<OperatorCredential> builder)
        {
            _ = builder.HasKey(c => c.Id);
            _ = builder.Property(c => c.Label).IsRequired().HasMaxLength(256);
            _ = builder.Property(c => c.WebAuthnCredentialId).IsRequired().HasMaxLength(512);
            _ = builder.Property(c => c.RevokedReason).HasMaxLength(512);

            _ = builder.HasIndex(c => c.OperatorId);
            _ = builder.HasIndex(c => c.WebAuthnCredentialId);
        }
    }
}
