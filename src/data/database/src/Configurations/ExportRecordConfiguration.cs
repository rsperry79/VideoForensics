using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ExportRecord entity.</summary>
    public class ExportRecordConfiguration : IEntityTypeConfiguration<ExportRecord>
    {
        public void Configure(EntityTypeBuilder<ExportRecord> builder)
        {
            _ = builder.HasKey(er => er.Id);

            _ = builder.Property(er => er.ExportedByUserName)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.Property(er => er.CaseReference)
                .HasMaxLength(256);

            _ = builder.Property(er => er.RecipientDescription)
                .HasMaxLength(512);

            _ = builder.Property(er => er.ArchiveFileName)
                .IsRequired()
                .HasMaxLength(1024);

            _ = builder.Property(er => er.ArchiveSha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(er => er.AppVersion)
                .IsRequired()
                .HasMaxLength(256);

            _ = builder.HasIndex(er => er.ExportedAtUtc);
        }
    }
}
