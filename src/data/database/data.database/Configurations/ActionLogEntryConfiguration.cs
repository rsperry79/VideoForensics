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
            builder.HasKey(ale => ale.Id);

            builder.Property(ale => ale.Actor)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(ale => ale.Action)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(ale => ale.EntityType)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(ale => ale.PreviousEntryHash)
                .HasMaxLength(64);

            builder.Property(ale => ale.EntryHash)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(ale => ale.TimestampUtc);
            builder.HasIndex(ale => new { ale.EntityType, ale.EntityId });
        }
    }
}
