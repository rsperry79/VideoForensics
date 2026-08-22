using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Video.Metadata.Models;

#nullable enable

namespace VideoForensics.Providers.Ring.Video.Metadata
{
    /// <summary>
    /// Extracts and tags frames from Ring videos at detection timestamps.
    /// Uses FFmpeg for reliable, platform-agnostic frame extraction.
    /// Creates visual evidence timeline for DV documentation.
    /// </summary>
    public class VideoFrameExtractor : IVideoFrameExtractor
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _ffmpegPath;

        /// <summary>
        /// Creates a frame extractor with optional custom FFmpeg path.
        /// If FFmpeg path not specified, assumes 'ffmpeg' is in PATH.
        /// </summary>
        public VideoFrameExtractor(IFileSystem? fileSystem = null, string? ffmpegPath = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _ffmpegPath = ffmpegPath ?? "ffmpeg";
        }

        public async Task<List<ExtractedFrame>> ExtractDetectionFramesAsync(
            string videoFilePath,
            VideoMetadata metadata,
            string outputDirectory)
        {
            return await Task.FromResult(ExtractDetectionFrames(videoFilePath, metadata, outputDirectory));
        }

        public async Task<List<ExtractedFrame>> ExtractFramesAtTimestampsAsync(
            string videoFilePath,
            List<long> timestamps,
            string outputDirectory)
        {
            return await Task.FromResult(ExtractFramesAtTimestamps(videoFilePath, timestamps, outputDirectory));
        }

        public List<ExtractedFrame> ExtractDetectionFrames(
            string videoFilePath,
            VideoMetadata metadata,
            string outputDirectory)
        {
            var frames = new List<ExtractedFrame>();

            // Validate inputs
            if (string.IsNullOrWhiteSpace(videoFilePath) || !_fileSystem.File.Exists(videoFilePath))
            {
                return frames;
            }

            // Ensure output directory exists
            if (!_fileSystem.Directory.Exists(outputDirectory))
            {
                _fileSystem.Directory.CreateDirectory(outputDirectory);
            }

            // Extract frames at verified detection timestamps if available
            if (metadata.VerifiedDetectionTimestamps != null && metadata.VerifiedDetectionTimestamps.Any())
            {
                frames.AddRange(ExtractFramesAtTimestamps(videoFilePath, metadata.VerifiedDetectionTimestamps, outputDirectory));

                // Tag each frame with metadata
                TagExtractedFrames(frames, metadata);
            }

            return frames;
        }

        public List<ExtractedFrame> ExtractFramesAtTimestamps(
            string videoFilePath,
            List<long> timestamps,
            string outputDirectory)
        {
            var frames = new List<ExtractedFrame>();

            if (!timestamps.Any())
            {
                return frames;
            }

            foreach (var timestampMs in timestamps.Distinct().OrderBy(ts => ts))
            {
                var frame = ExtractFrameAtTimestamp(videoFilePath, timestampMs, outputDirectory);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }

        private ExtractedFrame? ExtractFrameAtTimestamp(
            string videoFilePath,
            long timestampMs,
            string outputDirectory)
        {
            var startTime = DateTime.UtcNow;
            var timeFormatted = FormatTimestamp(timestampMs);
            var frameFileName = $"frame_{timeFormatted.Replace(":", "-").Replace(".", "_")}.jpg";
            var frameFilePath = _fileSystem.Path.Combine(outputDirectory, frameFileName);

            try
            {
                // Use FFmpeg to extract frame at timestamp
                // -ss seeks to timestamp before decoding (fast)
                // -vframes 1 extracts exactly 1 frame
                // -f image2 outputs image format
                var arguments = $"-ss {timeFormatted} -i \"{videoFilePath}\" -vframes 1 -f image2 \"{frameFilePath}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        return new ExtractedFrame
                        {
                            TimestampMs = timestampMs,
                            TimeFormatted = timeFormatted,
                            FrameFileName = frameFileName,
                            FrameFilePath = frameFilePath,
                            ExtractionSuccessful = false,
                            ExtractionError = "Failed to start FFmpeg process",
                            ExtractedAt = DateTime.UtcNow
                        };
                    }

                    process.WaitForExit(30000); // 30 second timeout

                    if (process.ExitCode != 0)
                    {
                        return new ExtractedFrame
                        {
                            TimestampMs = timestampMs,
                            TimeFormatted = timeFormatted,
                            FrameFileName = frameFileName,
                            FrameFilePath = frameFilePath,
                            ExtractionSuccessful = false,
                            ExtractionError = $"FFmpeg exited with code {process.ExitCode}",
                            ExtractedAt = DateTime.UtcNow
                        };
                    }
                }

                // Verify extraction succeeded
                if (!_fileSystem.File.Exists(frameFilePath))
                {
                    return new ExtractedFrame
                    {
                        TimestampMs = timestampMs,
                        TimeFormatted = timeFormatted,
                        FrameFileName = frameFileName,
                        FrameFilePath = frameFilePath,
                        ExtractionSuccessful = false,
                        ExtractionError = "Frame file not created",
                        ExtractedAt = DateTime.UtcNow
                    };
                }

                var fileInfo = _fileSystem.FileInfo.New(frameFilePath);

                return new ExtractedFrame
                {
                    TimestampMs = timestampMs,
                    TimeFormatted = timeFormatted,
                    FrameFileName = frameFileName,
                    FrameFilePath = frameFilePath,
                    FileSizeBytes = fileInfo.Length,
                    ExtractionSuccessful = true,
                    ExtractedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ExtractedFrame
                {
                    TimestampMs = timestampMs,
                    TimeFormatted = timeFormatted,
                    FrameFileName = frameFileName,
                    FrameFilePath = frameFilePath,
                    ExtractionSuccessful = false,
                    ExtractionError = ex.Message,
                    ExtractedAt = DateTime.UtcNow
                };
            }
        }

        private void TagExtractedFrames(List<ExtractedFrame> frames, VideoMetadata metadata)
        {
            foreach (var frame in frames)
            {
                // Tag with detection info from metadata
                frame.DetectionType = metadata.DetectionType;
                frame.DetectionConfidence = metadata.ModelConfidence ?? metadata.DetectionConfidence;
                frame.AnomalyScore = metadata.AnomalyScore;
                frame.RecognizedProfiles = metadata.RecognizedProfiles;
                frame.SecurityAlerts = metadata.SecurityAlerts;
                frame.ActiveZones = metadata.DetectionZones;
            }
        }

        private string FormatTimestamp(long timestampMs)
        {
            var seconds = timestampMs / 1000;
            var milliseconds = timestampMs % 1000;
            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;
            var secs = seconds % 60;

            return $"{hours:D2}:{minutes:D2}:{secs:D2}.{milliseconds:D3}";
        }
    }
}
