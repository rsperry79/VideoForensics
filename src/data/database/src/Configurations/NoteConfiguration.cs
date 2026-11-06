using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>
    /// Fluent API configuration for Note. There are no foreign keys: a note can target a case, a jamming incident or a media flag,
    /// so the target is checked by the repository for cases only. Notes are kept; they are never deleted.
    /// </summary>
    public class NoteConfiguration : IEntityTypeConfiguration<Note>
    {
        public void Configure(EntityTypeBuilder<Note> builder)
        {
            _ = builder.ToTable("Notes");

            _ = builder.HasKey(n => n.Id);

            // Stored by name so the column stays readable and survives enum reordering.
            _ = builder.Property(n => n.TargetKind)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(32);

            _ = builder.Property(n => n.Body)
                .IsRequired()
                .HasMaxLength(4000);

            _ = builder.Property(n => n.AddedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(n => new { n.TargetKind, n.TargetId, n.AddedAtUtc });
        }
    }
}
