using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for SnapshotCapture (one row per snapshot attempt).</summary>
    public class SnapshotCaptureConfiguration : IEntityTypeConfiguration<SnapshotCapture>
    {
        public void Configure(EntityTypeBuilder<SnapshotCapture> builder)
        {
            _ = builder.HasKey(c => c.Id);

            _ = builder.Property(c => c.Trigger)
                .IsRequired()
                .HasMaxLength(16);

            // SQLite cannot translate DateTimeOffset comparisons or ORDER BY, so the instant is stored as a
            // UTC DateTime (the convention the other time-series tables use). Entity stays DateTimeOffset.
            _ = builder.Property(c => c.CapturedAtUtc)
                .HasConversion(
                    v => v.UtcDateTime,
                    v => new DateTimeOffset(v, TimeSpan.Zero));

            _ = builder.Property(c => c.MediaFilePath)
                .HasMaxLength(1024);

            _ = builder.HasIndex(c => new { c.DeviceId, c.CapturedAtUtc });

            // Forensic history must not vanish with a device delete; retention will be an explicit policy later.
            _ = builder.HasOne<Device>()
                .WithMany()
                .HasForeignKey(c => c.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Link only: a health row being pruned must not block or erase the capture attempt.
            _ = builder.HasOne<DeviceHealth>()
                .WithMany()
                .HasForeignKey(c => c.DeviceHealthId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
