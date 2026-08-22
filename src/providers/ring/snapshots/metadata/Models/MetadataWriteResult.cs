using System;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Models
{
    /// <summary>
    /// Result of metadata writing/validation operation on a snapshot.
    /// </summary>
    public class MetadataWriteResult
    {
        /// <summary>
        /// Status of the metadata operation.
        /// </summary>
        public MetadataStatus Status { get; set; }

        /// <summary>
        /// Whether metadata was actually written to the file.
        /// </summary>
        public bool WasWritten { get; set; }

        /// <summary>
        /// Whether the snapshot is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether corrections were applied to the file.
        /// </summary>
        public bool WasCorrected { get; set; }

        /// <summary>
        /// List of corrections applied to the file.
        /// </summary>
        public List<string> CorrectionsApplied { get; set; } = new();

        /// <summary>
        /// Error message if processing failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Duration of the operation in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Timestamp when the operation was processed (UTC).
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// PhotoPrism-compatible tags extracted/generated from the snapshot.
        /// </summary>
        public List<string>? PhotoPrismTags { get; set; }
    }
}
