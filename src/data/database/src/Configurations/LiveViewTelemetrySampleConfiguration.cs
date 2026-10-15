using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for LiveViewTelemetrySample entity.</summary>
    public class LiveViewTelemetrySampleConfiguration : IEntityTypeConfiguration<LiveViewTelemetrySample>
    {
        public void Configure(EntityTypeBuilder<LiveViewTelemetrySample> builder)
        {
            _ = builder.HasKey(t => t.Id);

            _ = builder.HasOne<LiveViewSession>()
                .WithMany()
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasIndex(t => new { t.SessionId, t.CapturedAtUtc });
        }
    }
}
