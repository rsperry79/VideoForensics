using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
    {
        public void Configure(EntityTypeBuilder<AppSetting> builder)
        {
            _ = builder.HasKey(s => s.Id);
            _ = builder.HasIndex(s => s.Key).IsUnique();
            _ = builder.Property(s => s.Key).IsRequired().HasMaxLength(256);
            _ = builder.Property(s => s.Value).IsRequired();
            _ = builder.Property(s => s.UpdatedAtUtc).IsRequired();
        }
    }
}
