using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Notice entity.</summary>
    public class NoticeConfiguration : IEntityTypeConfiguration<Notice>
    {
        public void Configure(EntityTypeBuilder<Notice> builder)
        {
            _ = builder.HasKey(n => n.Id);
            _ = builder.Property(n => n.EventType).IsRequired().HasMaxLength(256);
            _ = builder.Property(n => n.Severity).IsRequired();
            _ = builder.Property(n => n.Details).HasMaxLength(2000);
            _ = builder.Property(n => n.Audience).IsRequired();
            _ = builder.Property(n => n.TimestampUtc).IsRequired();
            // OperatorId and ProviderAccountId are optional FKs with no explicit navigation property
        }
    }
}
