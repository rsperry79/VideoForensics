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
            _ = builder.HasKey(de => de.Id);

            _ = builder.Property(de => de.ProviderEventId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(de => de.EventType)
                .HasMaxLength(256);

            _ = builder.Property(de => de.RecordingStatus)
                .HasMaxLength(256);

            _ = builder.Property(de => de.ErrorMessage)
                .HasMaxLength(1024);

            _ = builder.Property(de => de.AppVersion)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(de => new { de.DeviceId, de.ProviderEventId })
                .IsUnique();

            _ = builder.HasIndex(de => de.DeviceId);
        }
    }
}
