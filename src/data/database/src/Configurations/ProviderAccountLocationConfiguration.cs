using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for the ProviderAccountLocation junction entity.</summary>
    public class ProviderAccountLocationConfiguration : IEntityTypeConfiguration<ProviderAccountLocation>
    {
        public void Configure(EntityTypeBuilder<ProviderAccountLocation> builder)
        {
            _ = builder.HasKey(x => new { x.ProviderAccountId, x.LocationId });

            _ = builder.HasOne(x => x.ProviderAccount)
                .WithMany()
                .HasForeignKey(x => x.ProviderAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasIndex(x => x.LocationId);
        }
    }
}
