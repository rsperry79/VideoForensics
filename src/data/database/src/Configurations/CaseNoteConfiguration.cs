using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for CaseNote. Notes are kept if their case is deleted: the FK restricts, it does not cascade.</summary>
    public class CaseNoteConfiguration : IEntityTypeConfiguration<CaseNote>
    {
        public void Configure(EntityTypeBuilder<CaseNote> builder)
        {
            _ = builder.ToTable("CaseNotes");

            _ = builder.HasKey(n => n.Id);

            _ = builder.Property(n => n.Body)
                .IsRequired()
                .HasMaxLength(4000);

            _ = builder.Property(n => n.AddedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(n => new { n.CaseId, n.AddedAtUtc });

            _ = builder.HasOne<ForensicCase>()
                .WithMany()
                .HasForeignKey(n => n.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
