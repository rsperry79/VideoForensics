namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A provider's device model (for example a Ring kind) with its product name and generation. Seeded from a reference source.</summary>
    public class DeviceModel
    {
        /// <summary>Raw provider kind, the primary key (for example <c>cocoa_doorbell_v5</c>).</summary>
        public required string Kind { get; set; }

        /// <summary>Provider that owns the row (for example "Ring"). Seeding only replaces rows for the provider it is loading.</summary>
        public required string Provider { get; set; }

        /// <summary>Product family (for example "Doorbell", "Camera", "Chime").</summary>
        public required string Family { get; set; }

        /// <summary>Marketing model name, null when no source ties the kind to a product.</summary>
        public string? ModelName { get; set; }

        /// <summary>Generation label as the provider names it, null when unknown.</summary>
        public string? Generation { get; set; }
    }

    /// <summary>A capability a device model can have (for example "camera", "chime", "wifi_bridge").</summary>
    public class DeviceFeatureDefinition
    {
        /// <summary>Stable code, the primary key.</summary>
        public required string Code { get; set; }

        /// <summary>Display name.</summary>
        public required string Name { get; set; }
    }

    /// <summary>Many-to-many link: one model has many features, and one feature applies to many models.</summary>
    public class DeviceModelFeature
    {
        public required string DeviceModelKind { get; set; }

        public required string FeatureCode { get; set; }
    }

    /// <summary>The version of each reference-data source that has been loaded into this database.</summary>
    public class ReferenceDataVersion
    {
        /// <summary>Source name, the primary key (for example "Ring").</summary>
        public required string SourceName { get; set; }

        public int Version { get; set; }

        public DateTime AppliedUtc { get; set; }
    }
}
