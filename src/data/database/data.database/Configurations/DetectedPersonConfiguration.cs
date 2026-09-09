using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for DetectedPerson entity.</summary>
    public class DetectedPersonConfiguration : IEntityTypeConfiguration<DetectedPerson>
    {
        public void Configure(EntityTypeBuilder<DetectedPerson> builder)
        {
            _ = builder.HasKey(d => d.Id);

            _ = builder.Property(d => d.ProfileId)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(d => d.ProfileName)
                .HasMaxLength(512);

            _ = builder.Property(d => d.ThumbnailUrl)
                .HasMaxLength(1024);

            _ = builder.HasIndex(d => new { d.MediaItemId, d.ProfileId });
        }
    }
}
