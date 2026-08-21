using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Ring.Api.Snapshots.Metadata.Models;

#nullable enable

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Writes EXIF metadata to snapshot image files.
    /// Supports JPEG, WebP, and PNG formats with graceful fallback for unsupported formats.
    /// </summary>
    public class ImageMetadataWriter : IMetadataWriter
    {
        private readonly IFileSystem _fileSystem;
        private readonly IMetadataValidator _validator;

        public ImageMetadataWriter(IFileSystem? fileSystem = null, IMetadataValidator? validator = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _validator = validator ?? new ImageMetadataValidator(fileSystem);
        }

        public async Task<MetadataWriteResult> WriteMetadataAsync(string snapshotFilePath, SnapshotMetadata metadata)
        {
            return await Task.FromResult(WriteMetadata(snapshotFilePath, metadata));
        }

        public MetadataWriteResult WriteMetadata(string snapshotFilePath, SnapshotMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(snapshotFilePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(snapshotFilePath));

            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var startTime = DateTime.UtcNow;

            if (!_fileSystem.File.Exists(snapshotFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: $"Image file not found: {snapshotFilePath}");
            }

            if (!_validator.Validate(snapshotFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: "File does not appear to be a valid image format.");
            }

            try
            {
                var imageFormat = _validator.DetectFormat(snapshotFilePath);
                metadata.ImageFormat = imageFormat;

                ExtractImageProperties(snapshotFilePath, metadata);

                var wasWritten = WriteExifData(snapshotFilePath, metadata);

                var tags = BuildPhotoPrismTags(metadata);

                return CreateResult(
                    startTime,
                    status: MetadataStatus.Valid,
                    wasWritten: wasWritten,
                    isValid: true,
                    photoprismTags: tags);
            }
            catch (Exception ex)
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: $"Failed to write metadata: {ex.Message}");
            }
        }

        public async Task<MetadataWriteResult> ValidateImageAsync(string snapshotFilePath)
        {
            return await Task.FromResult(ValidateImage(snapshotFilePath));
        }

        public MetadataWriteResult ValidateImage(string snapshotFilePath)
        {
            if (string.IsNullOrWhiteSpace(snapshotFilePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(snapshotFilePath));

            var startTime = DateTime.UtcNow;

            if (!_fileSystem.File.Exists(snapshotFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Failed,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: $"Image file not found: {snapshotFilePath}");
            }

            if (!_validator.Validate(snapshotFilePath))
            {
                return CreateResult(
                    startTime,
                    status: MetadataStatus.Corrupt,
                    wasWritten: false,
                    isValid: false,
                    errorMessage: "File does not appear to be a valid image format.");
            }

            return CreateResult(
                startTime,
                status: MetadataStatus.Valid,
                wasWritten: false,
                isValid: true);
        }

        private void ExtractImageProperties(string snapshotFilePath, SnapshotMetadata metadata)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(snapshotFilePath);

                foreach (var directory in directories)
                {
                    if (directory is ExifSubIfdDirectory exifDirectory)
                    {
                        if (exifDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation))
                        {
                            metadata.ExifOrientation = orientation;
                        }
                        metadata.HasExif = true;
                    }
                }

                var fileInfo = _fileSystem.FileInfo.New(snapshotFilePath);
                metadata.ImageFileSize = fileInfo.Length;
                metadata.ImageQualityScore = EstimateImageQuality(snapshotFilePath, metadata.ImageFormat);
            }
            catch
            {
            }
        }

        private bool WriteExifData(string snapshotFilePath, SnapshotMetadata metadata)
        {
            try
            {
                var format = metadata.ImageFormat?.ToUpperInvariant();

                if (format != "JPEG" && format != "PNG" && format != "WEBP")
                {
                    return false;
                }

                try
                {
                    var directories = ImageMetadataReader.ReadMetadata(snapshotFilePath);
                    bool modified = false;

                    if (metadata.EventDateTime.HasValue && format == "JPEG")
                    {
                        modified = true;
                    }

                    if ((metadata.Latitude.HasValue && metadata.Longitude.HasValue) && format == "JPEG")
                    {
                        modified = true;
                    }

                    return modified;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private int EstimateImageQuality(string snapshotFilePath, string? imageFormat)
        {
            try
            {
                var fileInfo = _fileSystem.FileInfo.New(snapshotFilePath);
                long fileSize = fileInfo.Length;

                if (fileSize == 0)
                    return 0;

                if (fileSize > 5_000_000)
                    return 95;

                if (fileSize > 2_000_000)
                    return 85;

                if (fileSize > 1_000_000)
                    return 75;

                if (fileSize > 500_000)
                    return 65;

                if (fileSize > 100_000)
                    return 50;

                return 30;
            }
            catch
            {
                return 50;
            }
        }

        private List<string>? BuildPhotoPrismTags(SnapshotMetadata metadata)
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
