using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for Operator entity.</summary>
    public class OperatorConfiguration : IEntityTypeConfiguration<Operator>
    {
        public void Configure(EntityTypeBuilder<Operator> builder)
        {
            _ = builder.HasKey(o => o.Id);
            _ = builder.Property(o => o.DisplayName).IsRequired().HasMaxLength(256);
        }
    }
}
