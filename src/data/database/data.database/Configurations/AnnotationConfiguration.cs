using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Annotation entity.</summary>
    public class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
    {
        public void Configure(EntityTypeBuilder<Annotation> builder)
        {
            _ = builder.HasKey(a => a.Id);

            _ = builder.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.Source)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.Key)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(a => a.Value)
                .IsRequired()
                .HasMaxLength(2048);

            _ = builder.HasIndex(a => new { a.EntityType, a.EntityId });

            _ = builder.HasIndex(a => new { a.Key, a.Value });
        }
    }
}
