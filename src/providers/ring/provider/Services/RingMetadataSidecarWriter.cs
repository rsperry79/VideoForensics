using Microsoft.Extensions.Logging;

using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Data.Common;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Writes and validates metadata sidecar files (.json) alongside downloaded media files.
    /// </summary>
    public interface IRingMetadataSidecarWriter
    {
        /// <summary>
        /// Writes metadata for a video event to a JSON sidecar file.
        /// </summary>
        bool WriteMetadataFile(string mediaFilePath, string deviceId, Entities.DoorbotHistoryEvent @event, long fileSizeBytes, string mediaFormat, Guid eventDbId);

        /// <summary>
        /// Writes metadata for a snapshot to a JSON sidecar file.
        /// </summary>
        bool WriteSnapshotMetadataFile(string mediaFilePath, string deviceId, long fileSizeBytes, Guid mediaItemId);

        /// <summary>
        /// Validates that a JSON sidecar file is well-formed and non-empty.
        /// </summary>
        bool ValidateJsonSidecar(string jsonPath, string metadataType);
    }

    public class RingMetadataSidecarWriter : IRingMetadataSidecarWriter
    {
        private readonly ILogger _logger;

        public RingMetadataSidecarWriter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool WriteMetadataFile(string mediaFilePath, string deviceId, Entities.DoorbotHistoryEvent @event, long fileSizeBytes, string mediaFormat, Guid eventDbId)
        {
            try
            {
                Entities.CvProperties cv = @event.CvProperties;
                var metadata = new RingEventMetadata(
                    FileName: Path.GetFileName(mediaFilePath),
                    DeviceId: deviceId,
                    DeviceName: @event.Doorbot?.Description ?? deviceId,
                    EventId: @event.Id?.ToString() ?? "unknown",
                    EventDbId: eventDbId,
                    EventType: @event.Kind,
                    Answered: @event.Answered,
                    Favorite: @event.Favorite,
                    RecordedAt: @event.CreatedAtDateTime ?? DateTime.MinValue,
                    FileSizeBytes: fileSizeBytes,
                    MediaFormat: mediaFormat,
                    RecordingStatus: @event.Recording?.Status,
                    SnapshotUrl: @event.SnapshotUrl,
                    LocationId: @event.Doorbot?.LocationId,
                    ComputerVision: cv == null ? null : new RingCvMetadata(
                        PersonDetected: cv.PersonDetected,
                        StreamBroken: cv.StreamBroken,
                        DetectionType: cv.DetectionType,
                        Detections: cv.DetectionTypes?
                            .Select(d => new RingCvDetection(d.DetectionType, d.VerifiedTimestamps))
                            .ToList(),
                        FullDescription: cv.FullDescription,
                        ShortDescription: cv.ShortDescription,
                        Similarity: cv.Similarity,
                        Anomaly: cv.Anomaly,
                        Tags: cv.Tags,
                        SecurityAlerts: cv.SecurityAlerts == null ? null :
                            new RingSecurityAlerts(cv.SecurityAlerts.Severity, cv.SecurityAlerts.Alerts),
                        Profiles: cv.Profiles?
                            .Select(p => new RingCvProfile(p.Id, p.Name, p.Confidence))
                            .ToList(),
                        Zones: cv.DetectionDetails?.Zones?
                            .Select(z => new RingCvZone(z.Id, z.Name, z.Confidence))
                            .ToList(),
                        DetectionConfidence: cv.DetectionDetails?.Confidence,
                        ModelVersion: cv.DetectionDetails?.ModelVersion
                    )
                );

                var metadataPath = Path.ChangeExtension(mediaFilePath, ".json");
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                File.WriteAllText(metadataPath, json);

                if (!ValidateJsonSidecar(metadataPath, "event metadata"))
                {
                    _logger.LogWarning("Validation failed for metadata sidecar {MetadataPath}", metadataPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write metadata file for {MediaFilePath}", mediaFilePath);
                return false;
            }
        }

        public bool WriteSnapshotMetadataFile(string mediaFilePath, string deviceId, long fileSizeBytes, Guid mediaItemId)
        {
            try
            {
                var metadata = new RingSnapshotMetadata(
                    FileName: Path.GetFileName(mediaFilePath),
                    DeviceId: deviceId,
                    MediaItemId: mediaItemId,
                    CapturedAt: DateTime.Now,
                    FileSizeBytes: fileSizeBytes,
                    MediaFormat: "jpg"
                );

                var metadataPath = Path.ChangeExtension(mediaFilePath, ".json");
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(metadataPath, json);

                if (!ValidateJsonSidecar(metadataPath, "snapshot metadata"))
                {
                    _logger.LogWarning("Validation failed for metadata sidecar {MetadataPath}", metadataPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write metadata file for {MediaFilePath}", mediaFilePath);
                return false;
            }
        }

        public bool ValidateJsonSidecar(string jsonPath, string metadataType)
        {
            try
            {
                // File exists and is readable
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning("JSON sidecar file does not exist: {JsonPath}", jsonPath);
                    return false;
                }

                var fileInfo = new FileInfo(jsonPath);

                // File is not empty (minimum valid JSON is {})
                if (fileInfo.Length < 2)
                {
                    _logger.LogWarning("JSON sidecar file is too small ({Size} bytes): {JsonPath}", fileInfo.Length, jsonPath);
                    return false;
                }

                // JSON is valid
                var content = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(content);
                // If we can parse it and get the root element, it's valid JSON
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    _logger.LogInformation("✓ Validated {MetadataType} sidecar: {JsonPath} ({Size} bytes)",
                        metadataType, Path.GetFileName(jsonPath), fileInfo.Length);
                    return true;
                }
                else
                {
                    _logger.LogWarning("JSON sidecar root is not an object: {JsonPath}", jsonPath);
                    return false;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in sidecar: {JsonPath}", jsonPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate JSON sidecar: {JsonPath}", jsonPath);
                return false;
            }
        }

        private static bool IsValidJpeg(string filePath)
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                if (stream.Length < 4)
                {
                    return false;
                }

                Span<byte> header = stackalloc byte[3];
                var read = stream.Read(header);
                // JPEG files start with the SOI marker: FF D8 FF
                return read == 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }
            catch
            {
                return false;
            }
        }

        private record RingEventMetadata(
            string FileName,
            string DeviceId,
            string DeviceName,
            string EventId,
            Guid EventDbId,
            string? EventType,
            bool Answered,
            bool Favorite,
            DateTime RecordedAt,
            long FileSizeBytes,
            string MediaFormat,
            string? RecordingStatus,
            string? SnapshotUrl,
            Guid? LocationId,
            RingCvMetadata? ComputerVision
        );

        /// <summary>Ring AI computer-vision analysis for an event, when available (not all events are CV-evaluated).</summary>
        private record RingCvMetadata(
            bool? PersonDetected,
            bool? StreamBroken,
            string? DetectionType,
            List<RingCvDetection>? Detections,
            string? FullDescription,
            string? ShortDescription,
            double? Similarity,
            double? Anomaly,
            List<string>? Tags,
            RingSecurityAlerts? SecurityAlerts,
            List<RingCvProfile>? Profiles,
            List<RingCvZone>? Zones,
            double? DetectionConfidence,
            string? ModelVersion
        );

        private record RingCvDetection(string? Type, List<long>? VerifiedTimestampsEpochMs);
        private record RingSecurityAlerts(string? Severity, List<string>? Alerts);
        private record RingCvProfile(string? Id, string? Name, double? Confidence);
        private record RingCvZone(string? Id, string? Name, double? Confidence);

        /// <summary>
        /// Metadata for a device's latest-snapshot download. Unlike video metadata, this isn't tied
        /// to a specific historical event — CapturedAt reflects when we fetched it, not necessarily
        /// when Ring's camera captured the underlying image (Ring doesn't expose that timestamp here).
        /// </summary>
        private record RingSnapshotMetadata(
            string FileName,
            string DeviceId,
            Guid MediaItemId,
            DateTime CapturedAt,
            long FileSizeBytes,
            string MediaFormat
        );
    }
}
