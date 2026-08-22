using System;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Video.Metadata.Models;

#nullable enable

namespace VideoForensics.Providers.Ring.Video.Metadata
{
    /// <summary>
    /// Extracts and saves video thumbnails from Ring snapshot data.
    /// Downloads snapshots and stores them alongside videos for evidence documentation.
    /// </summary>
    public class VideoThumbnailExtractor : IVideoThumbnailExtractor
    {
        private readonly IFileSystem _fileSystem;
        private readonly HttpClient _httpClient;

        public VideoThumbnailExtractor(IFileSystem? fileSystem = null, HttpClient? httpClient = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<VideoThumbnail?> ExtractAndSaveThumbnailAsync(
            string snapshotUrl,
            VideoMetadata videoMetadata,
            string videoFilePath,
            string outputDirectory)
        {
            return await Task.FromResult(ExtractAndSaveThumbnail(snapshotUrl, videoMetadata, videoFilePath, outputDirectory));
        }

        public VideoThumbnail? ExtractAndSaveThumbnail(
            string snapshotUrl,
            VideoMetadata videoMetadata,
            string videoFilePath,
            string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(snapshotUrl) || videoMetadata == null)
            {
                return null;
            }

            // Ensure output directory exists
            if (!_fileSystem.Directory.Exists(outputDirectory))
            {
                _fileSystem.Directory.CreateDirectory(outputDirectory);
            }

            var startTime = DateTime.UtcNow;

            // Generate thumbnail filename based on video filename
            var videoFileName = _fileSystem.Path.GetFileNameWithoutExtension(videoFilePath);
            var thumbnailFileName = $"{videoFileName}_thumbnail.jpg";
            var thumbnailPath = _fileSystem.Path.Combine(outputDirectory, thumbnailFileName);

            try
            {
                // Download snapshot from Ring
                using (var response = _httpClient.GetAsync(snapshotUrl).Result)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return new VideoThumbnail
                        {
                            SnapshotUrl = snapshotUrl,
                            FileName = thumbnailFileName,
                            FilePath = thumbnailPath,
                            VideoFilePath = videoFilePath,
                            ExtractionSuccessful = false,
                            ExtractionError = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                            ExtractedAt = DateTime.UtcNow
                        };
                    }

                    var content = response.Content.ReadAsByteArrayAsync().Result;

                    // Write thumbnail file
                    _fileSystem.File.WriteAllBytes(thumbnailPath, content);
                }

                // Verify file was written
                if (!_fileSystem.File.Exists(thumbnailPath))
                {
                    return new VideoThumbnail
                    {
                        SnapshotUrl = snapshotUrl,
                        FileName = thumbnailFileName,
                        FilePath = thumbnailPath,
                        VideoFilePath = videoFilePath,
                        ExtractionSuccessful = false,
                        ExtractionError = "Thumbnail file not written",
                        ExtractedAt = DateTime.UtcNow
                    };
                }

                var fileInfo = _fileSystem.FileInfo.New(thumbnailPath);

                // Create thumbnail info
                var thumbnail = new VideoThumbnail
                {
                    SnapshotUrl = snapshotUrl,
                    FileName = thumbnailFileName,
                    FilePath = thumbnailPath,
                    FileSizeBytes = fileInfo.Length,
                    ImageFormat = DetectImageFormat(thumbnailPath),
                    VideoFilePath = videoFilePath,
                    ExtractionSuccessful = true,
                    ExtractedAt = DateTime.UtcNow
                };

                return thumbnail;
            }
            catch (Exception ex)
            {
                return new VideoThumbnail
                {
                    SnapshotUrl = snapshotUrl,
                    FileName = thumbnailFileName,
                    FilePath = thumbnailPath,
                    VideoFilePath = videoFilePath,
                    ExtractionSuccessful = false,
                    ExtractionError = ex.Message,
                    ExtractedAt = DateTime.UtcNow
                };
            }
        }

        private string? DetectImageFormat(string filePath)
        {
            try
            {
                if (!_fileSystem.File.Exists(filePath))
                {
                    return null;
                }

                var bytes = new byte[12];
                using (var stream = _fileSystem.File.OpenRead(filePath))
                {
                    var bytesRead = stream.Read(bytes, 0, bytes.Length);
                    if (bytesRead < 3)
                    {
                        return null;
                    }
                }

                // Check for JPEG
                if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                {
                    return "JPEG";
                }

                // Check for PNG
                if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                {
                    return "PNG";
                }

                // Check for WebP
                if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                    bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                {
                    return "WebP";
                }

                // Check for GIF
                if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                {
                    return "GIF";
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
