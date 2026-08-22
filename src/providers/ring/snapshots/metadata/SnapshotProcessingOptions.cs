namespace VideoForensics.Providers.Ring.Snapshots.Metadata
{
    /// <summary>
    /// Configuration options for snapshot metadata processing.
    /// Allows enabling/disabling metadata extraction, EXIF writing, validation, and corrections.
    /// </summary>
    public class SnapshotProcessingOptions
    {
        /// <summary>
        /// Whether to extract metadata from snapshot events.
        /// Default: true
        /// </summary>
        public bool ExtractMetadata { get; set; } = true;

        /// <summary>
        /// Whether to write EXIF metadata to image files.
        /// Default: true
        /// This is the PRIMARY user control for EXIF writing.
        /// </summary>
        public bool WriteExif { get; set; } = true;

        /// <summary>
        /// Whether to validate image integrity and format.
        /// Default: true
        /// </summary>
        public bool ValidateImages { get; set; } = true;

        /// <summary>
        /// Whether to automatically correct common image issues (orientation, etc.).
        /// Default: true
        /// </summary>
        public bool AutoCorrect { get; set; } = true;

        /// <summary>
        /// Whether to generate PhotoPrism-compatible tags and metadata.
        /// Default: true
        /// </summary>
        public bool PhotoPrismCompatibility { get; set; } = true;

        /// <summary>
        /// Whether to include GPS coordinates in metadata.
        /// Privacy control - can be disabled to strip location data.
        /// Default: true
        /// </summary>
        public bool IncludeGps { get; set; } = true;

        /// <summary>
        /// Whether to include device street address in metadata.
        /// Privacy control - can be disabled to strip address data.
        /// Default: true
        /// </summary>
        public bool IncludeAddress { get; set; } = true;

        /// <summary>
        /// Whether to include device health metrics (battery, RSSI).
        /// Privacy control - can be disabled to strip device telemetry.
        /// Default: true
        /// </summary>
        public bool IncludeDeviceHealth { get; set; } = true;

        /// <summary>
        /// Whether to include AI analysis data (person detection, motion, confidence).
        /// Privacy control - can be disabled to strip analysis metadata.
        /// Default: true
        /// </summary>
        public bool IncludeAiAnalysis { get; set; } = true;

        /// <summary>
        /// Creates options with all metadata extraction and writing enabled.
        /// </summary>
        public static SnapshotProcessingOptions CreateDefault()
        {
            return new SnapshotProcessingOptions
            {
                ExtractMetadata = true,
                WriteExif = true,
                ValidateImages = true,
                AutoCorrect = true,
                PhotoPrismCompatibility = true,
                IncludeGps = true,
                IncludeAddress = true,
                IncludeDeviceHealth = true,
                IncludeAiAnalysis = true
            };
        }

        /// <summary>
        /// Creates options focused on privacy with minimal data collection.
        /// Disables GPS, address, device health, and AI analysis.
        /// </summary>
        public static SnapshotProcessingOptions CreatePrivacyFocused()
        {
            return new SnapshotProcessingOptions
            {
                ExtractMetadata = true,
                WriteExif = true,
                ValidateImages = true,
                AutoCorrect = true,
                PhotoPrismCompatibility = false,
                IncludeGps = false,
                IncludeAddress = false,
                IncludeDeviceHealth = false,
                IncludeAiAnalysis = false
            };
        }

        /// <summary>
        /// Creates options with minimal processing - only basic validation.
        /// </summary>
        public static SnapshotProcessingOptions CreateMinimal()
        {
            return new SnapshotProcessingOptions
            {
                ExtractMetadata = false,
                WriteExif = false,
                ValidateImages = true,
                AutoCorrect = false,
                PhotoPrismCompatibility = false,
                IncludeGps = false,
                IncludeAddress = false,
                IncludeDeviceHealth = false,
                IncludeAiAnalysis = false
            };
        }

        /// <summary>
        /// Creates options with all processing disabled (no-op mode).
        /// </summary>
        public static SnapshotProcessingOptions CreateDisabled()
        {
            return new SnapshotProcessingOptions
            {
                ExtractMetadata = false,
                WriteExif = false,
                ValidateImages = false,
                AutoCorrect = false,
                PhotoPrismCompatibility = false,
                IncludeGps = false,
                IncludeAddress = false,
                IncludeDeviceHealth = false,
                IncludeAiAnalysis = false
            };
        }
    }
}
