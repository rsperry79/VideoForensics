using System;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Validates snapshot image files for format, corruption, and integrity.
    /// Uses file header inspection for platform-agnostic format detection.
    /// </summary>
    public class ImageMetadataValidator : IMetadataValidator
    {
        private readonly IFileSystem _fileSystem;

        public ImageMetadataValidator(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<bool> ValidateAsync(string snapshotFilePath)
        {
            return await Task.FromResult(Validate(snapshotFilePath));
        }

        public bool Validate(string snapshotFilePath)
        {
            if (string.IsNullOrWhiteSpace(snapshotFilePath))
                return false;

            if (!_fileSystem.File.Exists(snapshotFilePath))
                return false;

            var extension = _fileSystem.Path.GetExtension(snapshotFilePath).ToLowerInvariant();
            var validExtensions = new[] { ".jpg", ".jpeg", ".webp", ".png", ".gif" };

            if (Array.IndexOf(validExtensions, extension) < 0)
                return false;

            return !IsCorrupted(snapshotFilePath);
        }

        public string? DetectFormat(string snapshotFilePath)
        {
            if (string.IsNullOrWhiteSpace(snapshotFilePath) || !_fileSystem.File.Exists(snapshotFilePath))
                return null;

            try
            {
                using var file = _fileSystem.FileStream.New(snapshotFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                var headerBytes = new byte[12];
                var bytesRead = file.Read(headerBytes, 0, 12);

                if (bytesRead >= 2)
                {
                    if (headerBytes[0] == 0xFF && headerBytes[1] == 0xD8)
                        return "JPEG";
                }

                if (bytesRead >= 8)
                {
                    if (headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
                        return "PNG";

                    if (headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46 &&
                        headerBytes[8] == 0x57 && headerBytes[9] == 0x45 && headerBytes[10] == 0x42 && headerBytes[11] == 0x50)
                        return "WebP";
                }

                if (bytesRead >= 3)
                {
                    if (headerBytes[0] == 0x47 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46)
                        return "GIF";
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public bool IsCorrupted(string snapshotFilePath)
        {
            if (string.IsNullOrWhiteSpace(snapshotFilePath) || !_fileSystem.File.Exists(snapshotFilePath))
                return true;

            try
            {
                using var file = _fileSystem.FileStream.New(snapshotFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                var headerBytes = new byte[12];
                var bytesRead = file.Read(headerBytes, 0, 12);

                if (bytesRead == 0)
                    return true;

                var extension = _fileSystem.Path.GetExtension(snapshotFilePath).ToLowerInvariant();
                var detectedFormat = DetectFormat(snapshotFilePath);

                if (detectedFormat == null)
                    return true;

                // Check if detected format matches file extension
                bool formatMatchesExtension = ((extension == ".jpg" || extension == ".jpeg") && detectedFormat == "JPEG") ||
                                              (extension == ".png" && detectedFormat == "PNG") ||
                                              (extension == ".webp" && detectedFormat == "WebP") ||
                                              (extension == ".gif" && detectedFormat == "GIF");

                if (!formatMatchesExtension)
                    return true;

                if (detectedFormat == "JPEG")
                {
                    if (bytesRead >= 2 && headerBytes[0] == 0xFF && headerBytes[1] == 0xD8)
                        return false;
                }
                else if (detectedFormat == "PNG")
                {
                    if (bytesRead >= 4 && headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
                        return false;
                }
                else if (detectedFormat == "WebP")
                {
                    if (bytesRead >= 4 && headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46)
                        return false;
                }
                else if (detectedFormat == "GIF")
                {
                    if (bytesRead >= 3 && headerBytes[0] == 0x47 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46)
                        return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}
