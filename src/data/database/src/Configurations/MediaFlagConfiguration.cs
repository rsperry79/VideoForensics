using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for MediaFlag: review state that must outlive its media item.</summary>
    public class MediaFlagConfiguration : IEntityTypeConfiguration<MediaFlag>
    {
        public void Configure(EntityTypeBuilder<MediaFlag> builder)
        {
            _ = builder.HasKey(f => f.Id);

            _ = builder.Property(f => f.FlagType).IsRequired().HasConversion<string>().HasMaxLength(64);
            _ = builder.Property(f => f.Severity).IsRequired().HasConversion<string>().HasMaxLength(16);
            _ = builder.Property(f => f.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
            _ = builder.Property(f => f.ReviewedBy).HasMaxLength(256);
            _ = builder.Property(f => f.ReviewNote).HasMaxLength(1024);

            // Read-only on the entity: the repository fills it from the clip and its download event on each read.
            _ = builder.Ignore(f => f.EventType);

            // SQLite has no row version type, so the int is bumped by the repository and checked on every update.
            _ = builder.Property(f => f.RowVersion).IsConcurrencyToken();

            // The flag survives deleting its clip: the link is cleared and DeviceId plus the window still identify it.
            // Purge is a soft flag on the clip and never touches this row.
            _ = builder.HasOne<MediaItem>()
                .WithMany()
                .HasForeignKey(f => f.MediaItemId)
                .OnDelete(DeleteBehavior.SetNull);

            _ = builder.HasIndex(f => new { f.MediaItemId, f.FlagType })
                .IsUnique()
                .HasDatabaseName("UX_MediaFlags_MediaItemId_FlagType");

            _ = builder.HasIndex(f => new { f.DeviceId, f.FlagType, f.WindowStartUtc })
                .IsUnique()
                .HasDatabaseName("UX_MediaFlags_DeviceId_FlagType_WindowStartUtc");

            _ = builder.HasIndex(f => new { f.Status, f.DeviceId, f.RaisedAtUtc })
                .HasDatabaseName("IX_MediaFlags_Status_DeviceId_RaisedAtUtc");

            _ = builder.ToTable(t => t.HasCheckConstraint("CK_MediaFlags_Window", "\"WindowEndUtc\" >= \"WindowStartUtc\""));
        }
    }
}
