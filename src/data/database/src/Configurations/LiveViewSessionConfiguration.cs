using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for LiveViewSession entity.</summary>
    public class LiveViewSessionConfiguration : IEntityTypeConfiguration<LiveViewSession>
    {
        public void Configure(EntityTypeBuilder<LiveViewSession> builder)
        {
            _ = builder.HasKey(l => l.Id);

            _ = builder.Property(l => l.StopReason)
                .HasMaxLength(200);

            _ = builder.Property(l => l.PromotionReason)
                .HasMaxLength(200);

            _ = builder.Property(l => l.ProviderSessionRef)
                .HasMaxLength(200);

            _ = builder.HasIndex(l => l.DeviceId);
            _ = builder.HasIndex(l => l.State);
        }
    }
}
