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
            builder.HasKey(eri => eri.Id);

            builder.Property(eri => eri.MediaItemSha256HashAtExport)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(eri => eri.ExportRecordId);
            builder.HasIndex(eri => eri.MediaItemId);
        }
    }
}
