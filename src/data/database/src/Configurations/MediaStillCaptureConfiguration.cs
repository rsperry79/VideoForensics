using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for MediaStillCapture (1:1 derived-still provenance keyed by MediaItemId).</summary>
    public class MediaStillCaptureConfiguration : IEntityTypeConfiguration<MediaStillCapture>
    {
        public void Configure(EntityTypeBuilder<MediaStillCapture> builder)
        {
            _ = builder.HasKey(c => c.MediaItemId);

            _ = builder.Property(c => c.SourceSha256AtCapture)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(c => c.CapturedByOperator)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(c => c.CaptureMethod)
                .IsRequired()
                .HasConversion<string>();

            _ = builder.HasIndex(c => c.SourceMediaItemId)
                .HasDatabaseName("IX_MediaStillCaptures_SourceMediaItemId");

            // The derived still's own MediaItem row - deleting the still deletes its provenance too.
            _ = builder.HasOne<MediaItem>()
                .WithOne()
                .HasForeignKey<MediaStillCapture>(c => c.MediaItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // The source video's MediaItem row - restrict deletion of a source that has derived stills
            // (evidence provenance must not be silently orphaned).
            _ = builder.HasOne<MediaItem>()
                .WithMany()
                .HasForeignKey(c => c.SourceMediaItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
