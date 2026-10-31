using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for RingDeviceFeatureState. One row per device per Ring feature key, replaced per sync.</summary>
    public class RingDeviceFeatureStateConfiguration : IEntityTypeConfiguration<RingDeviceFeatureState>
    {
        public void Configure(EntityTypeBuilder<RingDeviceFeatureState> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.Property(s => s.FeatureKey).IsRequired().HasMaxLength(64);
            _ = builder.Property(s => s.Score).HasMaxLength(64);
            _ = builder.Property(s => s.Learning).HasMaxLength(128);
            _ = builder.Property(s => s.RecordingMode).HasMaxLength(64);
            _ = builder.Property(s => s.RecordingState).HasMaxLength(64);
            _ = builder.Property(s => s.ContinuousUnavailabilityReason).HasMaxLength(128);
            _ = builder.HasIndex(s => new { s.DeviceId, s.FeatureKey }).IsUnique();
            _ = builder.HasMany(s => s.Reasons).WithOne().HasForeignKey(r => r.FeatureStateId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Fluent API configuration for RingDeviceFeatureReason.</summary>
    public class RingDeviceFeatureReasonConfiguration : IEntityTypeConfiguration<RingDeviceFeatureReason>
    {
        public void Configure(EntityTypeBuilder<RingDeviceFeatureReason> builder)
        {
            _ = builder.HasKey(r => r.Id);
            _ = builder.Property(r => r.Kind).IsRequired().HasMaxLength(32);
            _ = builder.Property(r => r.Reason).IsRequired().HasMaxLength(128);
            _ = builder.HasIndex(r => r.FeatureStateId);
        }
    }
}
