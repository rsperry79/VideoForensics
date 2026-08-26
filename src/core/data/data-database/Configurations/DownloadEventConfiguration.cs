using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DownloadEvent entity.</summary>
    public class DownloadEventConfiguration : IEntityTypeConfiguration<DownloadEvent>
    {
        public void Configure(EntityTypeBuilder<DownloadEvent> builder)
        {
            builder.HasKey(de => de.Id);

            builder.Property(de => de.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(de => de.EventType)
                .HasMaxLength(256);

            builder.Property(de => de.RecordingStatus)
                .HasMaxLength(256);

            builder.Property(de => de.ErrorMessage)
                .HasMaxLength(1024);

            builder.Property(de => de.AppVersion)
                .IsRequired()
                .HasMaxLength(256);

            builder.HasIndex(de => new { de.DeviceId, de.ProviderEventId })
                .IsUnique();

            builder.HasIndex(de => de.DeviceId);
        }
    }
}
