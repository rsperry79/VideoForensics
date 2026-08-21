using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Ring.Api.Video.Metadata.Models;

#nullable enable

namespace Ring.Api.Video.Metadata
{
    /// <summary>
    /// No-operation metadata writer that validates files without modifying them.
    /// Platform-agnostic implementation using System.IO.Abstractions for testability.
    /// Useful when metadata tagging tools (FFmpeg) are not available.
    /// </summary>
    public class NoOpMetadataWriter : IMetadataWriter
    {
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Creates a metadata writer with optional custom IFileSystem for testing.
        /// </summary>
        /// <param name="fileSystem">Optional file system abstraction (uses real file system if null).</param>
        public NoOpMetadataWriter(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<MetadataWriteResult> WriteMetadataAsync(string videoFilePath, VideoMetadata metadata)
        {
            return await Task.FromResult(WriteMetadata(videoFilePath, metadata));
        }

        public MetadataWriteResult WriteMetadata(string videoFilePath, VideoMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath))
            {
                throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var startTime = DateTime.UtcNow;

            if (!_fileSystem.File.Exists(videoFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: $"Video file not found: {videoFilePath}");
            }

            if (!IsValidVideoFile(videoFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: "File does not appear to be a valid video format.");
            }

            // No-op: just validate that file exists and is readable
            return CreateResult(
                startTime,
                status: MetadataStatus.Valid,
                wasWritten: false,
                isValid: true,
                photoprismTags: BuildPhotoPrismTags(metadata));
        }

        public async Task<MetadataWriteResult> ValidateVideoAsync(string videoFilePath)
        {
            return await Task.FromResult(ValidateVideo(videoFilePath));
        }

        public MetadataWriteResult ValidateVideo(string videoFilePath)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath))
            {
                throw new ArgumentException("Video file path cannot be null or empty.", nameof(videoFilePath));
            }

            var startTime = DateTime.UtcNow;

            if (!_fileSystem.File.Exists(videoFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: $"Video file not found: {videoFilePath}");
            }

            if (!IsValidVideoFile(videoFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Corrupt,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: "File does not appear to be a valid video format.");
            }

            return CreateResult(
                startTime,
                status: MetadataStatus.Valid,
                wasWritten: false,
                isValid: true);
        }

        private bool IsValidVideoFile(string filePath)
        {
            if (!_fileSystem.File.Exists(filePath))
            {
                return false;
            }

            var extension = _fileSystem.Path.GetExtension(filePath).ToLowerInvariant();
            var validExtensions = new[] { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v" };

            if (Array.IndexOf(validExtensions, extension) < 0)
            {
                return false;
            }

            // Check file header for invalid file format signatures
            try
            {
                using var file = _fileSystem.FileStream.New(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                var headerBytes = new byte[4];
                var bytesRead = file.Read(headerBytes, 0, 4);

                // Detect obvious non-video formats even if file is small
                // JPEG: 0xFFD8FF
                if (bytesRead >= 3 && headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
                {
                    return false; // JPEG file
                }

                // PNG: 0x89504E47
                if (bytesRead >= 4 && headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
                {
                    return false; // PNG file
                }

                // GIF: 0x474946
                if (bytesRead >= 3 && headerBytes[0] == 0x47 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46)
                {
                    return false; // GIF file
                }

                if (bytesRead == 0)
                {
                    return false; // Empty file
                }

                return true; // Assume valid if not detected as another format
            }
            catch
            {
                return false;
            }
        }

        private List<string>? BuildPhotoPrismTags(VideoMetadata metadata)
        {
            var tags = new List<string>();

            if (!string.IsNullOrWhiteSpace(metadata.EventType))
            {
                tags.Add(metadata.EventType);
            }

            if (metadata.PersonDetected == true)
            {
                tags.Add("person");
            }

            if (metadata.MotionDetected == true)
            {
                tags.Add("motion");
            }

            return tags.Count > 0 ? tags : null;
        }

        private MetadataWriteResult CreateResult(
            DateTime startTime,
            MetadataStatus status,
            bool wasWritten,
            bool isValid,
            string? errorMessage = null,
            List<string>? photoprismTags = null)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new MetadataWriteResult
            {
                Status = status,
                WasWritten = wasWritten,
                IsValid = isValid,
                WasCorrected = false,
                ErrorMessage = errorMessage,
                DurationMs = (long)duration,
                ProcessedAt = DateTime.UtcNow,
                PhotoPrismTags = photoprismTags
            };
        }
    }
}
