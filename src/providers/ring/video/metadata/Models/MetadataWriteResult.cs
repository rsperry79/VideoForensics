using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Video.Metadata.Models
{
    /// <summary>
    /// Result of writing and validating metadata to a video file.
    /// </summary>
    public class MetadataWriteResult
    {
        /// <summary>
        /// The overall status of metadata processing.
        /// </summary>
        [JsonPropertyName("status")]
        public MetadataStatus Status { get; set; }

        /// <summary>
        /// Whether metadata was successfully written to the file.
        /// </summary>
        [JsonPropertyName("was_written")]
        public bool WasWritten { get; set; }

        /// <summary>
        /// Whether the file passed validation after metadata writing.
        /// </summary>
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether the file was corrected (e.g., codec or frame rate issues fixed).
        /// </summary>
        [JsonPropertyName("was_corrected")]
        public bool WasCorrected { get; set; }

        /// <summary>
        /// List of corrections applied to the file.
        /// </summary>
        [JsonPropertyName("corrections_applied")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? CorrectionsApplied { get; set; }

        /// <summary>
        /// Error message if metadata processing failed.
        /// </summary>
        [JsonPropertyName("error_message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Duration in milliseconds for the metadata operation.
        /// </summary>
        [JsonPropertyName("duration_ms")]
        public long DurationMs { get; set; }

        /// <summary>
        /// Timestamp when metadata processing completed.
        /// </summary>
        [JsonPropertyName("processed_at")]
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Tags extracted and written to the file for PhotoPrism compatibility.
        /// </summary>
        [JsonPropertyName("photoprism_tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? PhotoPrismTags { get; set; }
    }
}
