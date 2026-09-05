using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for PairedDevice entity.</summary>
    public class PairedDeviceConfiguration : IEntityTypeConfiguration<PairedDevice>
    {
        public void Configure(EntityTypeBuilder<PairedDevice> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.Property(d => d.DeviceName).IsRequired().HasMaxLength(256);
            _ = builder.Property(d => d.WebAuthnCredentialId).HasMaxLength(512);
            _ = builder.Property(d => d.FallbackApiKeyHash).HasMaxLength(128);
            _ = builder.Property(d => d.PinnedCertificateFingerprint).HasMaxLength(128);
            _ = builder.Property(d => d.RevokedReason).HasMaxLength(512);
            _ = builder.Property(d => d.LastSeenIp).HasMaxLength(64);

            _ = builder.HasIndex(d => d.OperatorId);
            _ = builder.HasIndex(d => d.WebAuthnCredentialId);
            _ = builder.HasIndex(d => d.FallbackApiKeyHash);
        }
    }
}
