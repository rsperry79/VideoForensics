using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Fluent API configuration for NoticeDismissal entity.</summary>
    public class NoticeDismissalConfiguration : IEntityTypeConfiguration<NoticeDismissal>
    {
        public void Configure(EntityTypeBuilder<NoticeDismissal> builder)
        {
            _ = builder.HasKey(nd => nd.Id);
            _ = builder.Property(nd => nd.NoticeId).IsRequired();
            _ = builder.Property(nd => nd.OperatorId).IsRequired();
            _ = builder.Property(nd => nd.DismissedUtc).IsRequired();
            _ = builder.HasIndex(nd => new { nd.NoticeId, nd.OperatorId }).IsUnique();
        }
    }
}
