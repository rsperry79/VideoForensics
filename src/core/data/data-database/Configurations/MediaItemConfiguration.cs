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
            builder.HasKey(m => m.Id);

            builder.Property(m => m.FileName)
                .IsRequired()
                .HasMaxLength(512);

            builder.Property(m => m.FilePath)
                .IsRequired()
                .HasMaxLength(1024);

            builder.Property(m => m.MediaFormat)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(m => m.Sha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(m => m.VideoCodec)
                .HasMaxLength(64);

            builder.Property(m => m.AudioCodec)
                .HasMaxLength(64);

            builder.Property(m => m.Resolution)
                .HasMaxLength(64);

            builder.Property(m => m.PurgeReason)
                .HasMaxLength(256);

            builder.HasIndex(m => m.Sha256Hash);

            builder.HasIndex(m => m.DeviceId);
            builder.HasIndex(m => m.DownloadEventId);
        }
    }
}
