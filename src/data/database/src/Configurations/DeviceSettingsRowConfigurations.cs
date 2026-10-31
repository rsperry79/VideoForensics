using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingDeviceSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class RingDeviceSettingsConfiguration : IEntityTypeConfiguration<RingDeviceSettings>
    {
        public void Configure(EntityTypeBuilder<RingDeviceSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.Property(s => s.RichNotificationsSceneSource).HasMaxLength(64);
            _ = builder.Property(s => s.TermsOfServiceAccepted).HasMaxLength(64);
            _ = builder.Property(s => s.RingMediaServerHost).HasMaxLength(256);
            _ = builder.Property(s => s.EncryptionEligibilityMode).HasMaxLength(64);
            _ = builder.Property(s => s.NaturalLanguageSearchMode).HasMaxLength(64);
            _ = builder.Property(s => s.ConciergeMode).HasMaxLength(64);
            _ = builder.Property(s => s.FlickElimRecommendedMode).HasMaxLength(64);
            _ = builder.Property(s => s.LiveViewPresetProfile).HasMaxLength(64);
            _ = builder.Property(s => s.MotionSnoozePresetProfile).HasMaxLength(64);
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<RingDeviceSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for DeviceGeneralSettings. One row per device (unique DeviceId), deleted with the device.</summary>
    public class DeviceGeneralSettingsConfiguration : IEntityTypeConfiguration<DeviceGeneralSettings>
    {
        public void Configure(EntityTypeBuilder<DeviceGeneralSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.DeviceId).IsUnique();
            _ = builder.HasOne<Device>().WithOne().HasForeignKey<DeviceGeneralSettings>(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
