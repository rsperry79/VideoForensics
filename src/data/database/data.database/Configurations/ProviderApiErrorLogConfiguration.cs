using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for ProviderApiErrorLog entity.</summary>
    public class ProviderApiErrorLogConfiguration : IEntityTypeConfiguration<ProviderApiErrorLog>
    {
        public void Configure(EntityTypeBuilder<ProviderApiErrorLog> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.RequestUrl)
                .HasMaxLength(2048);

            _ = builder.Property(e => e.ExceptionType)
                .HasMaxLength(128);

            _ = builder.Property(e => e.ErrorCategory)
                .IsRequired()
                .HasMaxLength(128);

            _ = builder.Property(e => e.ErrorMessage)
                .HasMaxLength(1024);

            _ = builder.Property(e => e.HttpMethod)
                .HasMaxLength(16);

            _ = builder.HasIndex(e => e.EventId);

            _ = builder.HasIndex(e => new { e.DeviceId, e.OccurredAtUtc });
        }
    }
}
