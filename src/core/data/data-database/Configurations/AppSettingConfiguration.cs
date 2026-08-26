using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
    {
        public void Configure(EntityTypeBuilder<AppSetting> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.Key).IsUnique();
            builder.Property(s => s.Key).IsRequired().HasMaxLength(256);
            builder.Property(s => s.Value).IsRequired();
            builder.Property(s => s.UpdatedAtUtc).IsRequired();
        }
    }
}
