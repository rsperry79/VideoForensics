using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingAccountProfile and its feature and flag children.</summary>
    public class RingAccountProfileConfiguration : IEntityTypeConfiguration<RingAccountProfile>
    {
        public void Configure(EntityTypeBuilder<RingAccountProfile> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.Property(p => p.Email).HasMaxLength(256);
            _ = builder.Property(p => p.FirstName).HasMaxLength(256);
            _ = builder.Property(p => p.LastName).HasMaxLength(256);
            _ = builder.Property(p => p.FirstNameExtra).HasMaxLength(256);
            _ = builder.Property(p => p.LastNameExtra).HasMaxLength(256);
            _ = builder.Property(p => p.PhoneNumber).HasMaxLength(64);
            _ = builder.Property(p => p.TfaPhoneNumber).HasMaxLength(64);
            _ = builder.Property(p => p.HardwareId).HasMaxLength(256);
            _ = builder.Property(p => p.ExplorerProgramTerms).HasMaxLength(256);
            _ = builder.Property(p => p.UserFlow).HasMaxLength(256);
            _ = builder.Property(p => p.AppBrand).HasMaxLength(256);
            _ = builder.Property(p => p.Country).HasMaxLength(16);
            _ = builder.Property(p => p.Status).HasMaxLength(64);
            _ = builder.Property(p => p.CreatedAt).HasMaxLength(64);
            _ = builder.Property(p => p.AccountType).HasMaxLength(64);
            _ = builder.Property(p => p.TsvState).HasMaxLength(64);
            _ = builder.HasIndex(p => p.RingAccountId).IsUnique();
            _ = builder.HasOne(p => p.SessionFeatures).WithOne().HasForeignKey<RingAccountSessionFeatures>(f => f.RingAccountProfileId).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasMany(p => p.Flags).WithOne().HasForeignKey(f => f.RingAccountProfileId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for the typed profile session feature flags.</summary>
    public class RingAccountSessionFeaturesConfiguration : IEntityTypeConfiguration<RingAccountSessionFeatures>
    {
        public void Configure(EntityTypeBuilder<RingAccountSessionFeatures> builder)
        {
            _ = builder.HasKey(f => f.Id);
        }
    }

    /// <summary>Fluent API configuration for a profile value without a dedicated column.</summary>
    public class RingAccountProfileFlagConfiguration : IEntityTypeConfiguration<RingAccountProfileFlag>
    {
        public void Configure(EntityTypeBuilder<RingAccountProfileFlag> builder)
        {
            _ = builder.HasKey(f => f.Id);
            _ = builder.Property(f => f.Scope).IsRequired().HasMaxLength(16);
            _ = builder.Property(f => f.Name).IsRequired().HasMaxLength(256);
            _ = builder.Property(f => f.ValueKind).IsRequired().HasMaxLength(16);
            _ = builder.HasIndex(f => f.RingAccountProfileId);
            _ = builder.HasMany(f => f.Items).WithOne().HasForeignKey(i => i.RingAccountProfileFlagId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one element of an array-valued profile flag.</summary>
    public class RingAccountProfileFlagItemConfiguration : IEntityTypeConfiguration<RingAccountProfileFlagItem>
    {
        public void Configure(EntityTypeBuilder<RingAccountProfileFlagItem> builder)
        {
            _ = builder.HasKey(i => i.Id);
            _ = builder.HasIndex(i => i.RingAccountProfileFlagId);
        }
    }

    /// <summary>Fluent API configuration for ringtones; rows are keyed by account and ringtone id.</summary>
    public class RingAccountRingtoneConfiguration : IEntityTypeConfiguration<RingAccountRingtone>
    {
        public void Configure(EntityTypeBuilder<RingAccountRingtone> builder)
        {
            _ = builder.HasKey(r => r.Id);
            _ = builder.Property(r => r.RingtoneId).HasMaxLength(256);
            _ = builder.Property(r => r.Description).HasMaxLength(512);
            _ = builder.Property(r => r.Kind).HasMaxLength(64);
            _ = builder.Property(r => r.Category).HasMaxLength(128);
            _ = builder.Property(r => r.Url).HasMaxLength(1024);
            _ = builder.Property(r => r.UrlAmz).HasMaxLength(1024);
            _ = builder.Property(r => r.UserId).HasMaxLength(256);
            _ = builder.HasIndex(r => new { r.RingAccountId, r.RingtoneId }).IsUnique();
            _ = builder.HasMany(r => r.SupportedDeviceKinds).WithOne().HasForeignKey(k => k.RingAccountRingtoneId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one device kind supported by a ringtone.</summary>
    public class RingAccountRingtoneDeviceKindConfiguration : IEntityTypeConfiguration<RingAccountRingtoneDeviceKind>
    {
        public void Configure(EntityTypeBuilder<RingAccountRingtoneDeviceKind> builder)
        {
            _ = builder.HasKey(k => k.Id);
            _ = builder.Property(k => k.DeviceKind).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(k => k.RingAccountRingtoneId);
        }
    }

    /// <summary>Fluent API configuration for the default ringtone ids; one row per account.</summary>
    public class RingAccountRingtoneDefaultsConfiguration : IEntityTypeConfiguration<RingAccountRingtoneDefaults>
    {
        public void Configure(EntityTypeBuilder<RingAccountRingtoneDefaults> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.Property(d => d.DefaultDingId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultDingUserId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultMotionId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultMotionUserId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultAlarmUserId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultChirpUserId).HasMaxLength(256);
            _ = builder.Property(d => d.DefaultAppstoreUserId).HasMaxLength(256);
            _ = builder.HasIndex(d => d.RingAccountId).IsUnique();
        }
    }

    /// <summary>Fluent API configuration for a location's Smart Lighting group set.</summary>
    public class LightGroupSetConfiguration : IEntityTypeConfiguration<LightGroupSet>
    {
        public void Configure(EntityTypeBuilder<LightGroupSet> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.ProviderLocationId).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(s => s.ProviderLocationId).IsUnique();
            _ = builder.HasMany(s => s.Groups).WithOne().HasForeignKey(g => g.LightGroupSetId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one Smart Lighting group and its device ids.</summary>
    public class LightGroupConfiguration : IEntityTypeConfiguration<LightGroup>
    {
        public void Configure(EntityTypeBuilder<LightGroup> builder)
        {
            _ = builder.HasKey(g => g.Id);
            _ = builder.Property(g => g.ProviderDeviceGroupId).HasMaxLength(128);
            _ = builder.Property(g => g.Name).HasMaxLength(256);
            _ = builder.HasIndex(g => g.LightGroupSetId);
            _ = builder.HasMany(g => g.Devices).WithOne().HasForeignKey(d => d.LightGroupId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one device id within a Smart Lighting group.</summary>
    public class LightGroupDeviceConfiguration : IEntityTypeConfiguration<LightGroupDevice>
    {
        public void Configure(EntityTypeBuilder<LightGroupDevice> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.Property(d => d.ProviderDeviceId).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(d => d.LightGroupId);
        }
    }

    /// <summary>Fluent API configuration for a location's current mode; one row per location.</summary>
    public class RingLocationModeConfiguration : IEntityTypeConfiguration<RingLocationMode>
    {
        public void Configure(EntityTypeBuilder<RingLocationMode> builder)
        {
            _ = builder.HasKey(m => m.Id);
            _ = builder.Property(m => m.ProviderLocationId).IsRequired().HasMaxLength(128);
            _ = builder.Property(m => m.Mode).HasMaxLength(32);
            _ = builder.Property(m => m.SecurityStatusMode).HasMaxLength(32);
            _ = builder.Property(m => m.ModePanelType).HasMaxLength(64);
            _ = builder.HasIndex(m => m.ProviderLocationId).IsUnique();
            _ = builder.HasMany(m => m.NotYetParticipating).WithOne().HasForeignKey(n => n.RingLocationModeId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one raw element of notYetParticipatingInMode.</summary>
    public class RingLocationModeNotYetParticipatingConfiguration : IEntityTypeConfiguration<RingLocationModeNotYetParticipating>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeNotYetParticipating> builder)
        {
            _ = builder.HasKey(n => n.Id);
            _ = builder.HasIndex(n => n.RingLocationModeId);
        }
    }

    /// <summary>Fluent API configuration for a location's mode settings; one row per location.</summary>
    public class RingLocationModeSettingsConfiguration : IEntityTypeConfiguration<RingLocationModeSettings>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeSettings> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.ProviderLocationId).IsRequired().HasMaxLength(128);
            _ = builder.Property(s => s.DefaultCameraOnlyMode).HasMaxLength(32);
            _ = builder.HasIndex(s => s.ProviderLocationId).IsUnique();
            _ = builder.HasMany(s => s.SupportedActionGroups).WithOne().HasForeignKey(g => g.RingLocationModeSettingsId).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasMany(s => s.Entries).WithOne().HasForeignKey(e => e.RingLocationModeSettingsId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one supported action group of the mode settings options.</summary>
    public class RingLocationModeSupportedActionGroupConfiguration : IEntityTypeConfiguration<RingLocationModeSupportedActionGroup>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeSupportedActionGroup> builder)
        {
            _ = builder.HasKey(g => g.Id);
            _ = builder.Property(g => g.GroupName).IsRequired().HasMaxLength(64);
            _ = builder.HasIndex(g => g.RingLocationModeSettingsId);
        }
    }

    /// <summary>Fluent API configuration for one device entry under one mode.</summary>
    public class RingLocationModeSettingsEntryConfiguration : IEntityTypeConfiguration<RingLocationModeSettingsEntry>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeSettingsEntry> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.Mode).IsRequired().HasMaxLength(32);
            _ = builder.Property(e => e.DeviceId).HasMaxLength(128);
            _ = builder.Property(e => e.DeviceIdType).HasMaxLength(64);
            _ = builder.HasIndex(e => e.RingLocationModeSettingsId);
            _ = builder.HasMany(e => e.Actions).WithOne().HasForeignKey(a => a.RingLocationModeSettingsEntryId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one enabled or supported action of a mode entry.</summary>
    public class RingLocationModeSettingsEntryActionConfiguration : IEntityTypeConfiguration<RingLocationModeSettingsEntryAction>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeSettingsEntryAction> builder)
        {
            _ = builder.HasKey(a => a.Id);
            _ = builder.Property(a => a.Kind).IsRequired().HasMaxLength(16);
            _ = builder.Property(a => a.ActionGroup).HasMaxLength(64);
            _ = builder.Property(a => a.ActionName).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(a => a.RingLocationModeSettingsEntryId);
        }
    }

    /// <summary>Fluent API configuration for a location's mode sharing state; one row per location.</summary>
    public class RingLocationModeSharingConfiguration : IEntityTypeConfiguration<RingLocationModeSharing>
    {
        public void Configure(EntityTypeBuilder<RingLocationModeSharing> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.ProviderLocationId).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(s => s.ProviderLocationId).IsUnique();
        }
    }
}
