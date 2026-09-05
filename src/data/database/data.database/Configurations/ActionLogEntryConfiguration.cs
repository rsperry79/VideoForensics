using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ActionLogEntry entity.</summary>
    public class ActionLogEntryConfiguration : IEntityTypeConfiguration<ActionLogEntry>
    {
        public void Configure(EntityTypeBuilder<ActionLogEntry> builder)
        {
            _ = builder.HasKey(ale => ale.Id);

            _ = builder.Property(ale => ale.Actor)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(ale => ale.Action)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(ale => ale.EntityType)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(ale => ale.PreviousEntryHash)
                .HasMaxLength(64);

            _ = builder.Property(ale => ale.EntryHash)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.HasIndex(ale => ale.TimestampUtc);
            _ = builder.HasIndex(ale => new { ale.EntityType, ale.EntityId });
        }
    }
}
