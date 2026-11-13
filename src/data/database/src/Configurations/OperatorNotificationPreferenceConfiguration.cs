using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for OperatorNotificationPreference entity.</summary>
    public class OperatorNotificationPreferenceConfiguration : IEntityTypeConfiguration<OperatorNotificationPreference>
    {
        public void Configure(EntityTypeBuilder<OperatorNotificationPreference> builder)
        {
            _ = builder.HasKey(onp => onp.OperatorId);
            _ = builder.Property(onp => onp.PushEnabled).IsRequired();
            _ = builder.Property(onp => onp.MinimumSeverity).IsRequired();
            _ = builder.Property(onp => onp.EmailEnabled).IsRequired().HasDefaultValue(false);
            _ = builder.Property(onp => onp.EmailEventTypes).IsRequired().HasMaxLength(4000).HasDefaultValue(string.Empty);
        }
    }
}
