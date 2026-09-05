using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for MediaItem entity.</summary>
    public class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
    {
        public void Configure(EntityTypeBuilder<MediaItem> builder)
        {
            _ = builder.HasKey(m => m.Id);

            _ = builder.Property(m => m.FileName)
                .IsRequired()
                .HasMaxLength(512);

            _ = builder.Property(m => m.FilePath)
                .IsRequired()
                .HasMaxLength(1024);

            _ = builder.Property(m => m.MediaFormat)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(m => m.Sha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(m => m.VideoCodec)
                .HasMaxLength(64);

            _ = builder.Property(m => m.AudioCodec)
                .HasMaxLength(64);

            _ = builder.Property(m => m.Resolution)
                .HasMaxLength(64);

            _ = builder.Property(m => m.PurgeReason)
                .HasMaxLength(256);

            _ = builder.HasIndex(m => m.Sha256Hash);

            _ = builder.HasIndex(m => m.DeviceId);
            _ = builder.HasIndex(m => m.DownloadEventId);
        }
    }
}
