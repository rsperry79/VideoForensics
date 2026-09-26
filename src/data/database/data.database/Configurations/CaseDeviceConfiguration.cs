using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for CaseDevice junction entity.</summary>
    public class CaseDeviceConfiguration : IEntityTypeConfiguration<CaseDevice>
    {
        public void Configure(EntityTypeBuilder<CaseDevice> builder)
        {
            _ = builder.HasKey(cd => new { cd.CaseId, cd.DeviceId });

            _ = builder.HasOne<ForensicCase>()
                .WithMany()
                .HasForeignKey(cd => cd.CaseId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne<Device>()
                .WithMany()
                .HasForeignKey(cd => cd.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
