using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Video.Metadata.Models;

namespace Ring.Api.Video.Metadata
{
    /// <summary>
    /// Interface for extracting and tagging frames from Ring videos at specific timestamps.
    /// Critical for DV evidence timelines - creates visual proof of AI detection moments.
    /// </summary>
    public interface IVideoFrameExtractor
    {
        /// <summary>
        /// Extracts frames from a video at specified timestamps and tags them with metadata.
        /// Creates timestamped evidence frames for DV documentation.
        /// </summary>
        /// <param name="videoFilePath">Path to the video file</param>
        /// <param name="metadata">Video metadata containing detection timestamps and profiles</param>
        /// <param name="outputDirectory">Directory to save extracted frames</param>
        /// <returns>List of extracted frame information with metadata tags</returns>
        Task<List<ExtractedFrame>> ExtractDetectionFramesAsync(
            string videoFilePath,
            VideoMetadata metadata,
            string outputDirectory);

        /// <summary>
        /// Extracts frames at specified timestamps.
        /// </summary>
        /// <param name="videoFilePath">Path to the video file</param>
        /// <param name="timestamps">Timestamps (epoch milliseconds) to extract</param>
        /// <param name="outputDirectory">Directory to save extracted frames</param>
        /// <returns>List of extracted frame information</returns>
        Task<List<ExtractedFrame>> ExtractFramesAtTimestampsAsync(
            string videoFilePath,
            List<long> timestamps,
            string outputDirectory);

        /// <summary>
        /// Synchronous version of ExtractDetectionFramesAsync.
        /// </summary>
        List<ExtractedFrame> ExtractDetectionFrames(
            string videoFilePath,
            VideoMetadata metadata,
            string outputDirectory);
    }

    /// <summary>
    /// Information about an extracted frame from a video.
    /// Used for evidence documentation and timeline reconstruction.
    /// </summary>
    public class ExtractedFrame
    {
        /// <summary>
        /// Timestamp when frame was extracted (epoch milliseconds).
        /// </summary>
        public long TimestampMs { get; set; }

        /// <summary>
        /// Human-readable timestamp in format HH:MM:SS.mmm.
        /// </summary>
        public string TimeFormatted { get; set; }

        /// <summary>
        /// File path where frame was saved (relative to output directory).
        /// </summary>
        public string FrameFileName { get; set; }

        /// <summary>
        /// Full path to extracted frame file.
        /// </summary>
        public string FrameFilePath { get; set; }

        /// <summary>
        /// Type of detection at this moment (person, motion, vehicle, etc.).
        /// </summary>
        public string DetectionType { get; set; }

        /// <summary>
        /// Confidence score for detections at this timestamp (0.0-1.0).
        /// </summary>
        public double? DetectionConfidence { get; set; }

        /// <summary>
        /// Anomaly score at this moment (0.0-1.0).
        /// Higher = more unusual/suspicious activity.
        /// </summary>
        public double? AnomalyScore { get; set; }

        /// <summary>
        /// Recognized profiles (people) visible in this frame.
        /// Critical for identifying perpetrators and witnesses.
        /// </summary>
        public List<DetectedProfile>? RecognizedProfiles { get; set; }

        /// <summary>
        /// Security alerts active at this timestamp.
        /// </summary>
        public List<string>? SecurityAlerts { get; set; }

        /// <summary>
        /// Motion zones with activity at this timestamp.
        /// Shows where in the frame activity occurred.
        /// </summary>
        public List<MotionZone>? ActiveZones { get; set; }

        /// <summary>
        /// Size of extracted frame file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// When the frame was extracted (UTC).
        /// </summary>
        public DateTime ExtractedAt { get; set; }

        /// <summary>
        /// Whether FFmpeg successfully extracted this frame.
        /// </summary>
        public bool ExtractionSuccessful { get; set; }

        /// <summary>
        /// Error message if extraction failed.
        /// </summary>
        public string? ExtractionError { get; set; }
    }
}
