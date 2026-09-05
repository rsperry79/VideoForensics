using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ExportRecordItem entity.</summary>
    public class ExportRecordItemConfiguration : IEntityTypeConfiguration<ExportRecordItem>
    {
        public void Configure(EntityTypeBuilder<ExportRecordItem> builder)
        {
            _ = builder.HasKey(eri => eri.Id);

            _ = builder.Property(eri => eri.MediaItemSha256HashAtExport)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.HasIndex(eri => eri.ExportRecordId);
            _ = builder.HasIndex(eri => eri.MediaItemId);
        }
    }
}
