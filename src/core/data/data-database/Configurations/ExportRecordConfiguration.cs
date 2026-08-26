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
            builder.HasKey(er => er.Id);

            builder.Property(er => er.ExportedByUserName)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(er => er.CaseReference)
                .HasMaxLength(256);

            builder.Property(er => er.RecipientDescription)
                .HasMaxLength(512);

            builder.Property(er => er.ArchiveFileName)
                .IsRequired()
                .HasMaxLength(1024);

            builder.Property(er => er.ArchiveSha256Hash)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(er => er.AppVersion)
                .IsRequired()
                .HasMaxLength(256);

            builder.HasIndex(er => er.ExportedAtUtc);
        }
    }
}
