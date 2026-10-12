using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for JammingScheduleWindow entity.</summary>
    public class JammingScheduleWindowConfiguration : IEntityTypeConfiguration<JammingScheduleWindow>
    {
        public void Configure(EntityTypeBuilder<JammingScheduleWindow> builder)
        {
            _ = builder.HasKey(jsw => jsw.Id);

            _ = builder.HasIndex(jsw => jsw.SyncScheduleId);
        }
    }
}
