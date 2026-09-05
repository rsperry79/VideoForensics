using System;
using System.IO.Abstractions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Models;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Extracts and processes snapshot frames from Ring events.
    /// Downloads snapshots and correlates with detected profiles and alerts.
    /// Critical for DV evidence documentation.
    /// </summary>
    public class SnapshotFrameExtractor : ISnapshotFrameExtractor
    {
        private readonly IFileSystem _fileSystem;
        private readonly HttpClient _httpClient;

        public SnapshotFrameExtractor(IFileSystem? fileSystem = null, HttpClient? httpClient = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<ProcessedSnapshot?> DownloadAndTagSnapshotAsync(
            string snapshotUrl,
            SnapshotMetadata metadata,
            string outputDirectory)
        {
            return await Task.FromResult(DownloadAndTagSnapshot(snapshotUrl, metadata, outputDirectory));
        }

        public ProcessedSnapshot? DownloadAndTagSnapshot(
            string snapshotUrl,
            SnapshotMetadata metadata,
            string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(snapshotUrl) || metadata == null)
            {
                return null;
            }

            // Ensure output directory exists
            if (!_fileSystem.Directory.Exists(outputDirectory))
            {
                _ = _fileSystem.Directory.CreateDirectory(outputDirectory);
            }

            _ = DateTime.UtcNow;
            var timeFormatted = FormatTimestamp(metadata.EventDateTime);
            var fileName = $"snapshot_{timeFormatted.Replace(":", "-").Replace(".", "_")}.jpg";
            var filePath = _fileSystem.Path.Combine(outputDirectory, fileName);

            try
            {
                // Download snapshot from Ring
                using (HttpResponseMessage response = _httpClient.GetAsync(snapshotUrl).Result)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return new ProcessedSnapshot
                        {
                            SnapshotUrl = snapshotUrl,
                            TimestampMs = ConvertToEpochMs(metadata.EventDateTime),
                            TimeFormatted = timeFormatted,
                            FileName = fileName,
                            FilePath = filePath,
                            ProcessingSuccessful = false,
                            ProcessingError = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                            ProcessedAt = DateTime.UtcNow
                        };
                    }

                    var content = response.Content.ReadAsByteArrayAsync().Result;

                    // Write snapshot file
                    _fileSystem.File.WriteAllBytes(filePath, content);
                }

                // Verify file was written
                if (!_fileSystem.File.Exists(filePath))
                {
                    return new ProcessedSnapshot
                    {
                        SnapshotUrl = snapshotUrl,
                        TimestampMs = ConvertToEpochMs(metadata.EventDateTime),
                        TimeFormatted = timeFormatted,
                        FileName = fileName,
                        FilePath = filePath,
                        ProcessingSuccessful = false,
                        ProcessingError = "Snapshot file not written",
                        ProcessedAt = DateTime.UtcNow
                    };
                }

                IFileInfo fileInfo = _fileSystem.FileInfo.New(filePath);

                // Create processed snapshot with metadata
                var processedSnapshot = new ProcessedSnapshot
                {
                    SnapshotUrl = snapshotUrl,
                    TimestampMs = ConvertToEpochMs(metadata.EventDateTime),
                    TimeFormatted = timeFormatted,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSizeBytes = fileInfo.Length,
                    ImageFormat = DetectImageFormat(filePath),
                    Dimensions = metadata.ImageDimensions,
                    DetectionType = metadata.DetectionType,
                    DetectionConfidence = metadata.DetectionConfidence,
                    AnomalyScore = metadata.AnomalyScore,
                    RecognizedProfiles = metadata.RecognizedProfiles,
                    SecurityAlerts = metadata.SecurityAlerts,
                    AlertSeverity = metadata.AlertSeverity,
                    ActiveZones = metadata.DetectionZones,
                    ProcessingSuccessful = true,
                    ProcessedAt = DateTime.UtcNow
                };

                return processedSnapshot;
            }
            catch (Exception ex)
            {
                return new ProcessedSnapshot
                {
                    SnapshotUrl = snapshotUrl,
                    TimestampMs = ConvertToEpochMs(metadata.EventDateTime),
                    TimeFormatted = timeFormatted,
                    FileName = fileName,
                    FilePath = filePath,
                    ProcessingSuccessful = false,
                    ProcessingError = ex.Message,
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<string?> GenerateEvidenceSummaryAsync(
            ProcessedSnapshot snapshot,
            SnapshotMetadata metadata,
            string outputDirectory)
        {
            return await Task.FromResult(GenerateEvidenceSummary(snapshot, metadata, outputDirectory));
        }

        private string? GenerateEvidenceSummary(
            ProcessedSnapshot snapshot,
            SnapshotMetadata metadata,
            string outputDirectory)
        {
            if (snapshot == null || !snapshot.ProcessingSuccessful)
            {
                return null;
            }

            try
            {
                var summaryFileName = $"summary_{snapshot.TimeFormatted.Replace(":", "-").Replace(".", "_")}.txt";
                var summaryPath = _fileSystem.Path.Combine(outputDirectory, summaryFileName);

                var summary = new StringBuilder();
                _ = summary.AppendLine("=== RING EVENT EVIDENCE SUMMARY ===");
                _ = summary.AppendLine();

                // Event Information
                _ = summary.AppendLine("EVENT INFORMATION:");
                _ = summary.AppendLine($"  Timestamp: {snapshot.TimeFormatted} (UTC)");
                _ = summary.AppendLine($"  Epoch (ms): {snapshot.TimestampMs}");
                _ = summary.AppendLine($"  Event ID: {metadata.RingEventId}");
                _ = summary.AppendLine($"  Event Kind: {metadata.RingEventKind}");
                _ = summary.AppendLine();

                // Device Information
                _ = summary.AppendLine("DEVICE INFORMATION:");
                _ = summary.AppendLine($"  Name: {metadata.DeviceName}");
                _ = summary.AppendLine($"  Manufacturer: {metadata.DeviceManufacturer}");
                _ = summary.AppendLine($"  Model: {metadata.DeviceModel}");
                _ = summary.AppendLine($"  Firmware: {metadata.DeviceFirmwareVersion}");
                _ = summary.AppendLine($"  Online: {metadata.DeviceOnline}");
                _ = summary.AppendLine($"  Notifications Enabled: {metadata.OwnerNotificationsEnabled}");
                _ = summary.AppendLine();

                // Location Information
                if (!string.IsNullOrWhiteSpace(metadata.Address) || metadata.Latitude.HasValue)
                {
                    _ = summary.AppendLine("LOCATION INFORMATION:");
                    _ = summary.AppendLine($"  Address: {metadata.Address}");
                    if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
                    {
                        _ = summary.AppendLine($"  Coordinates: {metadata.Latitude:F6}, {metadata.Longitude:F6}");
                    }

                    _ = summary.AppendLine($"  Timezone: {metadata.Timezone}");
                    _ = summary.AppendLine();
                }

                // Snapshot Information
                _ = summary.AppendLine("SNAPSHOT INFORMATION:");
                _ = summary.AppendLine($"  File: {snapshot.FileName}");
                _ = summary.AppendLine($"  Path: {snapshot.FilePath}");
                _ = summary.AppendLine($"  Size: {FormatFileSize(snapshot.FileSizeBytes)}");
                _ = summary.AppendLine($"  Format: {snapshot.ImageFormat}");
                _ = summary.AppendLine($"  Dimensions: {snapshot.Dimensions}");
                _ = summary.AppendLine();

                // Detection Information
                _ = summary.AppendLine("DETECTION INFORMATION:");
                _ = summary.AppendLine($"  Detection Type: {snapshot.DetectionType}");
                _ = summary.AppendLine($"  Confidence: {FormatConfidence(snapshot.DetectionConfidence)}");
                _ = summary.AppendLine($"  Anomaly Score: {FormatConfidence(snapshot.AnomalyScore)}");
                _ = summary.AppendLine();

                // Recognized Profiles
                if (snapshot.RecognizedProfiles != null && snapshot.RecognizedProfiles.Count > 0)
                {
                    _ = summary.AppendLine("RECOGNIZED PROFILES:");
                    foreach (DetectedProfile profile in snapshot.RecognizedProfiles)
                    {
                        _ = summary.AppendLine($"  - {profile.Name} (Confidence: {FormatConfidence(profile.Confidence)})");
                        if (!string.IsNullOrWhiteSpace(profile.Id))
                        {
                            _ = summary.AppendLine($"    ID: {profile.Id}");
                        }
                    }

                    _ = summary.AppendLine();
                }

                // Security Alerts
                if (snapshot.SecurityAlerts != null && snapshot.SecurityAlerts.Count > 0)
                {
                    _ = summary.AppendLine("SECURITY ALERTS:");
                    _ = summary.AppendLine($"  Severity: {snapshot.AlertSeverity}");
                    foreach (var alert in snapshot.SecurityAlerts)
                    {
                        _ = summary.AppendLine($"  - {alert}");
                    }

                    _ = summary.AppendLine();
                }

                // Motion Zones
                if (snapshot.ActiveZones != null && snapshot.ActiveZones.Count > 0)
                {
                    _ = summary.AppendLine("MOTION ZONES:");
                    foreach (MotionZone zone in snapshot.ActiveZones)
                    {
                        _ = summary.AppendLine($"  - {zone.Name} (Confidence: {FormatConfidence(zone.Confidence)})");
                    }

                    _ = summary.AppendLine();
                }

                // Device Health
                if (metadata.Rssi.HasValue || metadata.BatteryPercentage.HasValue)
                {
                    _ = summary.AppendLine("DEVICE HEALTH:");
                    if (metadata.Rssi.HasValue)
                    {
                        _ = summary.AppendLine($"  Signal (RSSI): {metadata.Rssi} dBm");
                    }

                    if (metadata.BatteryPercentage.HasValue)
                    {
                        _ = summary.AppendLine($"  Battery: {metadata.BatteryPercentage}%");
                    }

                    _ = summary.AppendLine();
                }

                // Write summary file
                _fileSystem.File.WriteAllText(summaryPath, summary.ToString());

                return summaryPath;
            }
            catch (Exception)
            {
                return null;
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
                using (FileSystemStream stream = _fileSystem.File.OpenRead(filePath))
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
                return bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 ? "GIF" : null;
            }
            catch
            {
                return null;
            }
        }

        private long ConvertToEpochMs(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return 0;
            }

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = dateTime.Value.ToUniversalTime() - epoch;
            return (long)diff.TotalMilliseconds;
        }

        private string FormatTimestamp(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return "00-00-00-000";
            }

            DateTime dt = dateTime.Value;
            return $"{dt:yyyy-MM-dd_HH-mm-ss}";
        }

        private string FormatConfidence(double? confidence)
        {
            return !confidence.HasValue ? "N/A" : $"{confidence.Value:P1}";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }
    }
}
