using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for CameraBitrateBaseline entity.</summary>
    public class CameraBitrateBaselineConfiguration : IEntityTypeConfiguration<CameraBitrateBaseline>
    {
        public void Configure(EntityTypeBuilder<CameraBitrateBaseline> builder)
        {
            _ = builder.HasKey(b => b.Id);

            _ = builder.HasIndex(b => new { b.DeviceId, b.HourOfDay, b.IsWeekend })
                .IsUnique();
        }
    }
}
