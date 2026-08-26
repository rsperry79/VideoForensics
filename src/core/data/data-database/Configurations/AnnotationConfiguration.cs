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
            builder.HasKey(a => a.Id);

            builder.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(a => a.Source)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(a => a.Key)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(a => a.Value)
                .IsRequired()
                .HasMaxLength(2048);

            builder.HasIndex(a => new { a.EntityType, a.EntityId });

            builder.HasIndex(a => new { a.Key, a.Value });
        }
    }
}
