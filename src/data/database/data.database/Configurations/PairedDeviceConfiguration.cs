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
            builder.HasKey(d => d.Id);
            builder.Property(d => d.DeviceName).IsRequired().HasMaxLength(256);
            builder.Property(d => d.WebAuthnCredentialId).HasMaxLength(512);
            builder.Property(d => d.FallbackApiKeyHash).HasMaxLength(128);
            builder.Property(d => d.PinnedCertificateFingerprint).HasMaxLength(128);
            builder.Property(d => d.RevokedReason).HasMaxLength(512);
            builder.Property(d => d.LastSeenIp).HasMaxLength(64);

            builder.HasIndex(d => d.OperatorId);
            builder.HasIndex(d => d.WebAuthnCredentialId);
            builder.HasIndex(d => d.FallbackApiKeyHash);
        }
    }
}
