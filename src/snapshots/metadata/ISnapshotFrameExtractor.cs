using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Snapshots.Metadata.Models;

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Interface for extracting and processing snapshot frames with metadata.
    /// Downloads Ring snapshots and correlates them with detected profiles and alerts.
    /// Critical for DV evidence documentation.
    /// </summary>
    public interface ISnapshotFrameExtractor
    {
        /// <summary>
        /// Downloads and tags the event snapshot with metadata.
        /// Creates a tagged evidence image for DV documentation.
        /// </summary>
        /// <param name="snapshotUrl">URL to the Ring snapshot</param>
        /// <param name="metadata">Snapshot metadata containing profiles and alerts</param>
        /// <param name="outputDirectory">Directory to save the snapshot</param>
        /// <returns>Information about the downloaded and tagged snapshot</returns>
        Task<ProcessedSnapshot?> DownloadAndTagSnapshotAsync(
            string snapshotUrl,
            SnapshotMetadata metadata,
            string outputDirectory);

        /// <summary>
        /// Synchronous version of DownloadAndTagSnapshotAsync.
        /// </summary>
        ProcessedSnapshot? DownloadAndTagSnapshot(
            string snapshotUrl,
            SnapshotMetadata metadata,
            string outputDirectory);

        /// <summary>
        /// Generates an evidence summary document for a snapshot.
        /// Creates a human-readable metadata report alongside the image.
        /// </summary>
        /// <param name="snapshot">The processed snapshot information</param>
        /// <param name="metadata">The associated metadata</param>
        /// <param name="outputDirectory">Directory to save the summary document</param>
        /// <returns>Path to generated summary document</returns>
        Task<string?> GenerateEvidenceSummaryAsync(
            ProcessedSnapshot snapshot,
            SnapshotMetadata metadata,
            string outputDirectory);
    }

    /// <summary>
    /// Information about a processed snapshot from a Ring event.
    /// Used for evidence documentation and investigation support.
    /// </summary>
    public class ProcessedSnapshot
    {
        /// <summary>
        /// Original Ring snapshot URL.
        /// </summary>
        public string? SnapshotUrl { get; set; }

        /// <summary>
        /// Timestamp of the snapshot event (epoch milliseconds).
        /// </summary>
        public long TimestampMs { get; set; }

        /// <summary>
        /// Human-readable timestamp in format HH:MM:SS.mmm.
        /// </summary>
        public string? TimeFormatted { get; set; }

        /// <summary>
        /// Local filename where snapshot was saved.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Full path to downloaded snapshot file.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Size of downloaded snapshot file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Image format (JPEG, WebP, PNG, etc.).
        /// </summary>
        public string? ImageFormat { get; set; }

        /// <summary>
        /// Image dimensions (width x height).
        /// </summary>
        public string? Dimensions { get; set; }

        /// <summary>
        /// Detected type at this moment (person, motion, vehicle, etc.).
        /// </summary>
        public string? DetectionType { get; set; }

        /// <summary>
        /// Confidence score for detections (0.0-1.0).
        /// </summary>
        public double? DetectionConfidence { get; set; }

        /// <summary>
        /// Anomaly score at this moment (0.0-1.0).
        /// Higher = more unusual/suspicious activity.
        /// </summary>
        public double? AnomalyScore { get; set; }

        /// <summary>
        /// Recognized profiles (people) visible in snapshot.
        /// CRITICAL: for identifying perpetrators and witnesses.
        /// </summary>
        public List<DetectedProfile>? RecognizedProfiles { get; set; }

        /// <summary>
        /// Security alerts active at snapshot time.
        /// </summary>
        public List<string>? SecurityAlerts { get; set; }

        /// <summary>
        /// Alert severity level if present.
        /// </summary>
        public string? AlertSeverity { get; set; }

        /// <summary>
        /// Motion zones with activity detected.
        /// Shows where in frame activity occurred.
        /// </summary>
        public List<MotionZone>? ActiveZones { get; set; }

        /// <summary>
        /// Whether download and processing was successful.
        /// </summary>
        public bool ProcessingSuccessful { get; set; }

        /// <summary>
        /// Error message if processing failed.
        /// </summary>
        public string? ProcessingError { get; set; }

        /// <summary>
        /// When the snapshot was downloaded and processed (UTC).
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Path to generated evidence summary document (if created).
        /// </summary>
        public string? EvidenceSummaryPath { get; set; }
    }
}
