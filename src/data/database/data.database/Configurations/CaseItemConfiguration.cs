using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for CaseItem entity with soft-deletion and integrity constraints.</summary>
    public class CaseItemConfiguration : IEntityTypeConfiguration<CaseItem>
    {
        public void Configure(EntityTypeBuilder<CaseItem> builder)
        {
            _ = builder.HasKey(ci => ci.Id);

            _ = builder.Property(ci => ci.Kind)
                .IsRequired()
                .HasConversion<string>();

            _ = builder.Property(ci => ci.Reason)
                .IsRequired()
                .HasMaxLength(1024);

            _ = builder.Property(ci => ci.AddedBy)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(ci => ci.RemovedBy)
                .HasMaxLength(256);

            _ = builder.Property(ci => ci.RemovalReason)
                .HasMaxLength(1024);

            _ = builder.Property(ci => ci.MediaSha256AtAdd)
                .HasMaxLength(64);

            // Unique index on (CaseId, EventId) for active (not soft-removed) items only.
            _ = builder.HasIndex(ci => new { ci.CaseId, ci.EventId })
                .IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL AND \"RemovedAtUtc\" IS NULL")
                .HasDatabaseName("IX_CaseItems_CaseId_EventId_Active");

            // Unique index on (CaseId, MediaItemId) for active (not soft-removed) items only.
            _ = builder.HasIndex(ci => new { ci.CaseId, ci.MediaItemId })
                .IsUnique()
                .HasFilter("\"MediaItemId\" IS NOT NULL AND \"RemovedAtUtc\" IS NULL")
                .HasDatabaseName("IX_CaseItems_CaseId_MediaItemId_Active");

            // Exactly one of EventId/MediaItemId must be set, matching Kind.
            _ = builder.ToTable(t => t.HasCheckConstraint(
                "CK_CaseItems_ExactlyOneTarget",
                "(\"Kind\" = 'Event' AND \"EventId\" IS NOT NULL AND \"MediaItemId\" IS NULL) OR " +
                "(\"Kind\" = 'Media' AND \"MediaItemId\" IS NOT NULL AND \"EventId\" IS NULL)"));

            // Foreign keys
            _ = builder.HasOne<ForensicCase>()
                .WithMany()
                .HasForeignKey(ci => ci.CaseId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne<Event>()
                .WithMany()
                .HasForeignKey(ci => ci.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = builder.HasOne<MediaItem>()
                .WithMany()
                .HasForeignKey(ci => ci.MediaItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
