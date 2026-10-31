using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for the Ring-only location details; one row per location, removed with the location.</summary>
    public class RingLocationDetailsConfiguration : IEntityTypeConfiguration<RingLocationDetails>
    {
        public void Configure(EntityTypeBuilder<RingLocationDetails> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.Property(d => d.GeoServiceVerified).HasMaxLength(64);
            _ = builder.Property(d => d.LocationType).HasMaxLength(64);
            _ = builder.Property(d => d.LocationResourceId).HasMaxLength(128);
            _ = builder.HasIndex(d => d.LocationId).IsUnique();
            _ = builder.HasOne<Location>().WithMany().HasForeignKey(d => d.LocationId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for a user shared onto a location; one row per user per location.</summary>
    public class RingLocationSharedUserConfiguration : IEntityTypeConfiguration<RingLocationSharedUser>
    {
        public void Configure(EntityTypeBuilder<RingLocationSharedUser> builder)
        {
            _ = builder.HasKey(u => u.Id);
            _ = builder.Property(u => u.FirstName).HasMaxLength(128);
            _ = builder.Property(u => u.LastName).HasMaxLength(128);
            _ = builder.Property(u => u.Email).HasMaxLength(256);
            _ = builder.Property(u => u.ObjectType).HasMaxLength(64);
            _ = builder.HasIndex(u => new { u.LocationId, u.ProviderUserId }).IsUnique();
            _ = builder.HasOne<Location>().WithMany().HasForeignKey(u => u.LocationId).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasMany(u => u.Devices).WithOne().HasForeignKey(d => d.RingLocationSharedUserId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one device grant of a shared user, ordered by Position.</summary>
    public class RingLocationSharedUserDeviceConfiguration : IEntityTypeConfiguration<RingLocationSharedUserDevice>
    {
        public void Configure(EntityTypeBuilder<RingLocationSharedUserDevice> builder)
        {
            _ = builder.HasKey(d => d.Id);
            _ = builder.Property(d => d.DeviceType).HasMaxLength(64);
            _ = builder.Property(d => d.Role).HasMaxLength(64);
            _ = builder.HasIndex(d => new { d.RingLocationSharedUserId, d.Position }).IsUnique();
            _ = builder.HasMany(d => d.Permissions).WithOne().HasForeignKey(p => p.RingLocationSharedUserDeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for one permission of a device grant, ordered by Position.</summary>
    public class RingLocationSharedUserDevicePermissionConfiguration : IEntityTypeConfiguration<RingLocationSharedUserDevicePermission>
    {
        public void Configure(EntityTypeBuilder<RingLocationSharedUserDevicePermission> builder)
        {
            _ = builder.HasKey(p => p.Id);
            _ = builder.Property(p => p.Permission).HasMaxLength(64);
            _ = builder.HasIndex(p => new { p.RingLocationSharedUserDeviceId, p.Position }).IsUnique();
        }
    }

    /// <summary>Fluent API configuration for a doorbot linked to a chime; rows are replaced per chime.</summary>
    public class RingChimeLinkedDoorbotConfiguration : IEntityTypeConfiguration<RingChimeLinkedDoorbot>
    {
        public void Configure(EntityTypeBuilder<RingChimeLinkedDoorbot> builder)
        {
            _ = builder.HasKey(l => l.Id);
            _ = builder.Property(l => l.DeviceId).HasMaxLength(128);
            _ = builder.Property(l => l.Kind).HasMaxLength(64);
            _ = builder.HasIndex(l => new { l.ChimeDeviceId, l.LinkedDoorbotId }).IsUnique();
            _ = builder.HasOne<Device>().WithMany().HasForeignKey(l => l.ChimeDeviceId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for the latest snapshot timestamp of one doorbot within a Ring account.</summary>
    public class RingSnapshotTimestampConfiguration : IEntityTypeConfiguration<RingSnapshotTimestamp>
    {
        public void Configure(EntityTypeBuilder<RingSnapshotTimestamp> builder)
        {
            _ = builder.HasKey(t => t.Id);
            _ = builder.HasIndex(t => new { t.RingAccountId, t.DoorbotId }).IsUnique();
            _ = builder.HasOne<RingAccount>().WithMany().HasForeignKey(t => t.RingAccountId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
