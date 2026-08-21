using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ring.Api.Entities;
using Ring.Api.Snapshots.Metadata.Models;

namespace Ring.Api.Snapshots.Metadata
{
    /// <summary>
    /// Extracts metadata from snapshot events into SnapshotMetadata objects.
    /// Handles GPS, device info, detection data, and generates PhotoPrism tags.
    /// </summary>
    public class SnapshotMetadataExtractor : IMetadataExtractor
    {
        private readonly SnapshotProcessingOptions _options;

        public SnapshotMetadataExtractor(SnapshotProcessingOptions? options = null)
        {
            _options = options ?? SnapshotProcessingOptions.CreateDefault();
        }

        public async Task<SnapshotMetadata> ExtractMetadataAsync(DoorbotHistoryEvent snapshotEvent)
        {
            return await Task.FromResult(ExtractMetadata(snapshotEvent));
        }

        public SnapshotMetadata ExtractMetadata(DoorbotHistoryEvent snapshotEvent)
        {
            if (snapshotEvent == null)
                throw new ArgumentNullException(nameof(snapshotEvent));

            var metadata = new SnapshotMetadata
            {
                EventDateTime = snapshotEvent.CreatedAtDateTime,
                RingEventId = snapshotEvent.Id?.ToString(),
                RingEventKind = snapshotEvent.Kind
            };

            if (_options.IncludeGps)
            {
                ExtractLocationInfo(snapshotEvent, metadata);
            }

            if (_options.IncludeDeviceHealth)
            {
                ExtractDeviceHealth(snapshotEvent, metadata);
            }

            ExtractDeviceInfo(snapshotEvent, metadata);

            if (_options.IncludeAiAnalysis)
            {
                ExtractCvProperties(snapshotEvent, metadata);
            }

            metadata.EventType = DetermineEventType(snapshotEvent);
            metadata.Keywords = BuildKeywords(metadata);
            metadata.Comment = BuildComment(metadata);

            if (_options.PhotoPrismCompatibility)
            {
                // Keywords already built for PhotoPrism
            }

            return metadata;
        }

        private void ExtractLocationInfo(DoorbotHistoryEvent snapshotEvent, SnapshotMetadata metadata)
        {
            var doorbot = snapshotEvent.Doorbot;
            if (doorbot == null)
                return;

            if (doorbot.Latitude.HasValue && doorbot.Longitude.HasValue)
            {
                metadata.Latitude = doorbot.Latitude;
                metadata.Longitude = doorbot.Longitude;
            }

            if (_options.IncludeAddress && !string.IsNullOrWhiteSpace(doorbot.Address))
            {
                metadata.Address = doorbot.Address;
            }

            if (!string.IsNullOrWhiteSpace(doorbot.TimeZone))
            {
                metadata.Timezone = doorbot.TimeZone;
            }
        }

        private void ExtractDeviceHealth(DoorbotHistoryEvent snapshotEvent, SnapshotMetadata metadata)
        {
            var doorbot = snapshotEvent.Doorbot;
            if (doorbot == null)
                return;

            if (doorbot.Health != null)
            {
                if (doorbot.Health.Rssi.HasValue)
                {
                    metadata.Rssi = (int)doorbot.Health.Rssi.Value;
                }

                if (doorbot.Health.BatteryPercentage.HasValue)
                {
                    metadata.BatteryPercentage = (int)doorbot.Health.BatteryPercentage.Value;
                }
            }
        }

        private void ExtractDeviceInfo(DoorbotHistoryEvent snapshotEvent, SnapshotMetadata metadata)
        {
            var doorbot = snapshotEvent.Doorbot;
            if (doorbot == null)
                return;

            metadata.DeviceName = doorbot.Description ?? doorbot.Kind;
            metadata.DeviceManufacturer = "Amazon";
            metadata.DeviceModel = DetermineDeviceModel(doorbot.Kind, doorbot.Type);
        }

        private void ExtractCvProperties(DoorbotHistoryEvent snapshotEvent, SnapshotMetadata metadata)
        {
            var cvProperties = snapshotEvent.CvProperties;
            if (cvProperties == null)
            {
                metadata.MotionDetected = true;
                return;
            }

            if (cvProperties.PersonDetected.HasValue)
            {
                metadata.PersonDetected = cvProperties.PersonDetected.Value;
            }

            if (!string.IsNullOrWhiteSpace(cvProperties.DetectionType))
            {
                metadata.DetectionType = cvProperties.DetectionType;
                metadata.MotionDetected = true;
            }
            else if (cvProperties.PersonDetected.HasValue && cvProperties.PersonDetected.Value)
            {
                metadata.MotionDetected = true;
            }

            if (cvProperties.Similarity.HasValue)
            {
                metadata.DetectionConfidence = (int)cvProperties.Similarity.Value;
            }
        }

        private string? DetermineEventType(DoorbotHistoryEvent snapshotEvent)
        {
            if (snapshotEvent.Kind == null)
                return null;

            return snapshotEvent.Kind.ToLowerInvariant() switch
            {
                "motion" => "motion",
                "person" => "person",
                "package" => "package",
                "visitor" => "visitor",
                _ => snapshotEvent.Kind.ToLowerInvariant()
            };
        }

        private string? DetermineDeviceModel(string? deviceKind, string? deviceType)
        {
            if (!string.IsNullOrWhiteSpace(deviceType))
            {
                return deviceType;
            }

            if (string.IsNullOrWhiteSpace(deviceKind))
                return null;

            return deviceKind.ToLowerInvariant() switch
            {
                "doorbot" => "Doorbell",
                "stickupcam" or "stickup" => "Stick Up Cam",
                "stickupcam_pro" => "Stick Up Cam Pro",
                "indoor_camera" => "Indoor Cam",
                "outdoor_camera" => "Outdoor Cam",
                "floodlight_camera" => "Floodlight Cam",
                "chime" => "Chime",
                "beams_lightgroup_v3" => "Smart Lighting",
                _ => deviceKind
            };
        }

        private List<string>? BuildKeywords(SnapshotMetadata metadata)
        {
            var keywords = new List<string>();

            if (!string.IsNullOrWhiteSpace(metadata.DeviceName))
            {
                keywords.Add(metadata.DeviceName.ToLowerInvariant().Replace(" ", "-"));
            }

            if (!string.IsNullOrWhiteSpace(metadata.EventType))
            {
                keywords.Add(metadata.EventType.ToLowerInvariant());
            }

            if (metadata.PersonDetected == true)
            {
                keywords.Add("person");
            }

            if (metadata.MotionDetected == true)
            {
                keywords.Add("motion");
            }

            if (!string.IsNullOrWhiteSpace(metadata.ImageFormat))
            {
                keywords.Add(metadata.ImageFormat.ToLowerInvariant());
            }

            return keywords.Count > 0 ? keywords : null;
        }

        private string? BuildComment(SnapshotMetadata metadata)
        {
            var parts = new List<string>();

            if (metadata.PersonDetected == true && metadata.DetectionConfidence.HasValue)
            {
                parts.Add($"Person detected ({metadata.DetectionConfidence}% confidence)");
            }
            else if (metadata.PersonDetected == true)
            {
                parts.Add("Person detected");
            }

            if (metadata.MotionDetected == true)
            {
                parts.Add("Motion detected");
            }

            if (!string.IsNullOrWhiteSpace(metadata.DeviceName))
            {
                parts.Add($"Device: {metadata.DeviceName}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }
    }
}
