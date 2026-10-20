using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Device models keyed by provider kind.</summary>
    public class DeviceModelConfiguration : IEntityTypeConfiguration<DeviceModel>
    {
        public void Configure(EntityTypeBuilder<DeviceModel> builder)
        {
            _ = builder.HasKey(m => m.Kind);
            _ = builder.HasIndex(m => m.Provider);
            _ = builder.Property(m => m.Source).IsRequired().HasMaxLength(16).HasDefaultValue(DeviceModelSources.Catalog);
        }
    }

    /// <summary>Feature definitions keyed by code.</summary>
    public class DeviceFeatureDefinitionConfiguration : IEntityTypeConfiguration<DeviceFeatureDefinition>
    {
        public void Configure(EntityTypeBuilder<DeviceFeatureDefinition> builder)
        {
            _ = builder.HasKey(f => f.Code);
        }
    }

    /// <summary>Model-to-feature links. The composite key makes each (model, feature) pair unique.</summary>
    public class DeviceModelFeatureConfiguration : IEntityTypeConfiguration<DeviceModelFeature>
    {
        public void Configure(EntityTypeBuilder<DeviceModelFeature> builder)
        {
            _ = builder.HasKey(l => new { l.DeviceModelKind, l.FeatureCode });
            _ = builder.HasOne<DeviceModel>().WithMany().HasForeignKey(l => l.DeviceModelKind).OnDelete(DeleteBehavior.Cascade);
            _ = builder.HasOne<DeviceFeatureDefinition>().WithMany().HasForeignKey(l => l.FeatureCode).OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Loaded reference-data version per source.</summary>
    public class ReferenceDataVersionConfiguration : IEntityTypeConfiguration<ReferenceDataVersion>
    {
        public void Configure(EntityTypeBuilder<ReferenceDataVersion> builder)
        {
            _ = builder.HasKey(v => v.SourceName);
        }
    }
}
