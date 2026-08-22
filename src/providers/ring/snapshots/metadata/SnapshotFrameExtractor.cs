using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Snapshots.Metadata.Models;

#nullable enable

namespace VideoForensics.Providers.Ring.Snapshots.Metadata
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
                _fileSystem.Directory.CreateDirectory(outputDirectory);
            }

            var startTime = DateTime.UtcNow;
            var timeFormatted = FormatTimestamp(metadata.EventDateTime);
            var fileName = $"snapshot_{timeFormatted.Replace(":", "-").Replace(".", "_")}.jpg";
            var filePath = _fileSystem.Path.Combine(outputDirectory, fileName);

            try
            {
                // Download snapshot from Ring
                using (var response = _httpClient.GetAsync(snapshotUrl).Result)
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

                var fileInfo = _fileSystem.FileInfo.New(filePath);

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
                summary.AppendLine("=== RING EVENT EVIDENCE SUMMARY ===");
                summary.AppendLine();

                // Event Information
                summary.AppendLine("EVENT INFORMATION:");
                summary.AppendLine($"  Timestamp: {snapshot.TimeFormatted} (UTC)");
                summary.AppendLine($"  Epoch (ms): {snapshot.TimestampMs}");
                summary.AppendLine($"  Event ID: {metadata.RingEventId}");
                summary.AppendLine($"  Event Kind: {metadata.RingEventKind}");
                summary.AppendLine();

                // Device Information
                summary.AppendLine("DEVICE INFORMATION:");
                summary.AppendLine($"  Name: {metadata.DeviceName}");
                summary.AppendLine($"  Manufacturer: {metadata.DeviceManufacturer}");
                summary.AppendLine($"  Model: {metadata.DeviceModel}");
                summary.AppendLine($"  Firmware: {metadata.DeviceFirmwareVersion}");
                summary.AppendLine($"  Online: {metadata.DeviceOnline}");
                summary.AppendLine($"  Notifications Enabled: {metadata.OwnerNotificationsEnabled}");
                summary.AppendLine();

                // Location Information
                if (!string.IsNullOrWhiteSpace(metadata.Address) || metadata.Latitude.HasValue)
                {
                    summary.AppendLine("LOCATION INFORMATION:");
                    summary.AppendLine($"  Address: {metadata.Address}");
                    if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
                    {
                        summary.AppendLine($"  Coordinates: {metadata.Latitude:F6}, {metadata.Longitude:F6}");
                    }
                    summary.AppendLine($"  Timezone: {metadata.Timezone}");
                    summary.AppendLine();
                }

                // Snapshot Information
                summary.AppendLine("SNAPSHOT INFORMATION:");
                summary.AppendLine($"  File: {snapshot.FileName}");
                summary.AppendLine($"  Path: {snapshot.FilePath}");
                summary.AppendLine($"  Size: {FormatFileSize(snapshot.FileSizeBytes)}");
                summary.AppendLine($"  Format: {snapshot.ImageFormat}");
                summary.AppendLine($"  Dimensions: {snapshot.Dimensions}");
                summary.AppendLine();

                // Detection Information
                summary.AppendLine("DETECTION INFORMATION:");
                summary.AppendLine($"  Detection Type: {snapshot.DetectionType}");
                summary.AppendLine($"  Confidence: {FormatConfidence(snapshot.DetectionConfidence)}");
                summary.AppendLine($"  Anomaly Score: {FormatConfidence(snapshot.AnomalyScore)}");
                summary.AppendLine();

                // Recognized Profiles
                if (snapshot.RecognizedProfiles != null && snapshot.RecognizedProfiles.Count > 0)
                {
                    summary.AppendLine("RECOGNIZED PROFILES:");
                    foreach (var profile in snapshot.RecognizedProfiles)
                    {
                        summary.AppendLine($"  - {profile.Name} (Confidence: {FormatConfidence(profile.Confidence)})");
                        if (!string.IsNullOrWhiteSpace(profile.Id))
                        {
                            summary.AppendLine($"    ID: {profile.Id}");
                        }
                    }
                    summary.AppendLine();
                }

                // Security Alerts
                if (snapshot.SecurityAlerts != null && snapshot.SecurityAlerts.Count > 0)
                {
                    summary.AppendLine("SECURITY ALERTS:");
                    summary.AppendLine($"  Severity: {snapshot.AlertSeverity}");
                    foreach (var alert in snapshot.SecurityAlerts)
                    {
                        summary.AppendLine($"  - {alert}");
                    }
                    summary.AppendLine();
                }

                // Motion Zones
                if (snapshot.ActiveZones != null && snapshot.ActiveZones.Count > 0)
                {
                    summary.AppendLine("MOTION ZONES:");
                    foreach (var zone in snapshot.ActiveZones)
                    {
                        summary.AppendLine($"  - {zone.Name} (Confidence: {FormatConfidence(zone.Confidence)})");
                    }
                    summary.AppendLine();
                }

                // Device Health
                if (metadata.Rssi.HasValue || metadata.BatteryPercentage.HasValue)
                {
                    summary.AppendLine("DEVICE HEALTH:");
                    if (metadata.Rssi.HasValue)
                    {
                        summary.AppendLine($"  Signal (RSSI): {metadata.Rssi} dBm");
                    }
                    if (metadata.BatteryPercentage.HasValue)
                    {
                        summary.AppendLine($"  Battery: {metadata.BatteryPercentage}%");
                    }
                    summary.AppendLine();
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

        private long ConvertToEpochMs(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return 0;
            }

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var diff = dateTime.Value.ToUniversalTime() - epoch;
            return (long)diff.TotalMilliseconds;
        }

        private string FormatTimestamp(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return "00-00-00-000";
            }

            var dt = dateTime.Value;
            return $"{dt:yyyy-MM-dd_HH-mm-ss}";
        }

        private string FormatConfidence(double? confidence)
        {
            if (!confidence.HasValue)
            {
                return "N/A";
            }

            return $"{confidence.Value:P1}";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }
    }
}
