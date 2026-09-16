using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for PushSubscription entity.</summary>
    public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
    {
        public void Configure(EntityTypeBuilder<PushSubscription> builder)
        {
            _ = builder.HasKey(ps => ps.Id);
            _ = builder.Property(ps => ps.OperatorId).IsRequired();
            _ = builder.Property(ps => ps.Endpoint).IsRequired().HasMaxLength(500);
            _ = builder.Property(ps => ps.P256dhKey).IsRequired().HasMaxLength(500);
            _ = builder.Property(ps => ps.AuthKey).IsRequired().HasMaxLength(500);
            _ = builder.Property(ps => ps.CreatedUtc).IsRequired();
            _ = builder.HasIndex(ps => ps.Endpoint).IsUnique();
        }
    }
}
