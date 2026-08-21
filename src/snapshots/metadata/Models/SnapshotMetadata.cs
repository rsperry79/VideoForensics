using System;
using System.Collections.Generic;
using Ring.Api.Entities;

namespace Ring.Api.Snapshots.Metadata.Models
{
    /// <summary>
    /// Metadata extracted from a snapshot event, including image-specific fields.
    /// Contains both Ring event data and EXIF-compatible image properties.
    /// </summary>
    public class SnapshotMetadata
    {
        /// <summary>
        /// DateTime of the snapshot event.
        /// </summary>
        public DateTime? EventDateTime { get; set; }

        /// <summary>
        /// Latitude of the device location.
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Longitude of the device location.
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Street address of the device location.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Timezone of the device.
        /// </summary>
        public string? Timezone { get; set; }

        /// <summary>
        /// RSSI (signal strength) of the device's network connection.
        /// </summary>
        public int? Rssi { get; set; }

        /// <summary>
        /// Battery percentage of the device.
        /// </summary>
        public int? BatteryPercentage { get; set; }

        /// <summary>
        /// Whether motion was detected in the snapshot.
        /// </summary>
        public bool? MotionDetected { get; set; }

        /// <summary>
        /// Whether a person was detected in the snapshot.
        /// </summary>
        public bool? PersonDetected { get; set; }

        /// <summary>
        /// Type of detection (e.g., "motion", "person").
        /// </summary>
        public string? DetectionType { get; set; }

        /// <summary>
        /// Confidence level of the detection (0-100).
        /// </summary>
        public int? DetectionConfidence { get; set; }

        /// <summary>
        /// Name of the Ring device.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Manufacturer of the device (typically "Amazon").
        /// </summary>
        public string? DeviceManufacturer { get; set; }

        /// <summary>
        /// Model of the Ring device.
        /// </summary>
        public string? DeviceModel { get; set; }

        /// <summary>
        /// Type of event (e.g., "motion", "person").
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// Keywords/tags for the snapshot for PhotoPrism compatibility.
        /// </summary>
        public List<string>? Keywords { get; set; }

        /// <summary>
        /// User comment or description for the snapshot.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Ring event kind identifier.
        /// </summary>
        public string? RingEventKind { get; set; }

        /// <summary>
        /// Ring event ID.
        /// </summary>
        public string? RingEventId { get; set; }

        // Image-Specific Fields

        /// <summary>
        /// Image format (JPEG, WebP, PNG).
        /// </summary>
        public string? ImageFormat { get; set; }

        /// <summary>
        /// Image dimensions (width × height).
        /// </summary>
        public string? ImageDimensions { get; set; }

        /// <summary>
        /// Image color space (sRGB, Adobe RGB, etc.).
        /// </summary>
        public string? ImageColorSpace { get; set; }

        /// <summary>
        /// Image file size in bytes.
        /// </summary>
        public long ImageFileSize { get; set; }

        /// <summary>
        /// Whether the image contains EXIF data.
        /// </summary>
        public bool HasExif { get; set; }

        /// <summary>
        /// EXIF orientation (1-8 for rotation values).
        /// </summary>
        public int ExifOrientation { get; set; } = 1;

        /// <summary>
        /// Estimated image quality score (1-100).
        /// </summary>
        public int ImageQualityScore { get; set; }
    }
}
