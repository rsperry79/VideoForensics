using System.Text.Json.Serialization;

#nullable enable

namespace Ring.Api.Video.Metadata
{
    /// <summary>
    /// Configuration options for video metadata processing.
    /// Controls whether metadata extraction, writing, and validation are enabled.
    /// </summary>
    public class MetadataProcessingOptions
    {
        /// <summary>
        /// Whether to extract metadata from Ring events.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("extract_metadata")]
        public bool ExtractMetadata { get; set; } = true;

        /// <summary>
        /// Whether to write extracted metadata to video files.
        /// When false, metadata is extracted but not written.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("write_metadata")]
        public bool WriteMetadata { get; set; } = true;

        /// <summary>
        /// Whether to validate video files after metadata processing.
        /// When false, no validation checks are performed.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("validate_media")]
        public bool ValidateMedia { get; set; } = true;

        /// <summary>
        /// Whether to attempt automatic correction of common video encoding issues.
        /// Corrections may include fixing codec issues, frame rate problems, etc.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("auto_correct")]
        public bool AutoCorrect { get; set; } = true;

        /// <summary>
        /// Whether to include PhotoPrism-compatible tags in metadata.
        /// These tags help organize videos by event type and detection results.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("photoprism_compatibility")]
        public bool PhotoPrismCompatibility { get; set; } = true;

        /// <summary>
        /// Whether to include GPS coordinates in metadata (latitude/longitude).
        /// When false, GPS data is excluded for privacy.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("include_gps")]
        public bool IncludeGps { get; set; } = true;

        /// <summary>
        /// Whether to include device address information in metadata.
        /// When false, address data is excluded for privacy.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("include_address")]
        public bool IncludeAddress { get; set; } = true;

        /// <summary>
        /// Whether to include device health telemetry (battery, signal strength).
        /// When false, health data is excluded.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("include_device_health")]
        public bool IncludeDeviceHealth { get; set; } = true;

        /// <summary>
        /// Whether to include AI/CV analysis results (person detection, etc.).
        /// When false, analysis results are excluded.
        /// Default: true.
        /// </summary>
        [JsonPropertyName("include_ai_analysis")]
        public bool IncludeAiAnalysis { get; set; } = true;

        /// <summary>
        /// Create default options with all features enabled.
        /// </summary>
        public static MetadataProcessingOptions CreateDefault() => new();

        /// <summary>
        /// Create privacy-focused options with sensitive data excluded.
        /// </summary>
        public static MetadataProcessingOptions CreatePrivacyFocused() => new()
        {
            IncludeGps = false,
            IncludeAddress = false,
            IncludeDeviceHealth = false
        };

        /// <summary>
        /// Create minimal options with only essential metadata processing.
        /// </summary>
        public static MetadataProcessingOptions CreateMinimal() => new()
        {
            ExtractMetadata = true,
            WriteMetadata = false,
            ValidateMedia = false,
            AutoCorrect = false,
            PhotoPrismCompatibility = true,
            IncludeGps = false,
            IncludeAddress = false,
            IncludeDeviceHealth = false,
            IncludeAiAnalysis = true
        };

        /// <summary>
        /// Create options with all processing disabled.
        /// </summary>
        public static MetadataProcessingOptions CreateDisabled() => new()
        {
            ExtractMetadata = false,
            WriteMetadata = false,
            ValidateMedia = false,
            AutoCorrect = false,
            PhotoPrismCompatibility = false,
            IncludeGps = false,
            IncludeAddress = false,
            IncludeDeviceHealth = false,
            IncludeAiAnalysis = false
        };
    }
}
