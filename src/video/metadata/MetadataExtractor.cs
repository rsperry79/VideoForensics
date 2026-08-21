using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ring.Api.Entities;
using Ring.Api.Video.Metadata.Models;

#nullable enable

namespace Ring.Api.Video.Metadata
{
    /// <inheritdoc cref="IMetadataExtractor"/>
    public class MetadataExtractor : IMetadataExtractor
    {
        private const string EventTypeMotion = "motion";
        private const string EventTypePerson = "person";
        private const string EventTypeRing = "ring";
        private const string EventTypeDoorbell = "doorbell";

        public async Task<VideoMetadata> ExtractMetadataAsync(DoorbotHistoryEvent ringEvent)
        {
            return await Task.FromResult(ExtractMetadata(ringEvent));
        }

        public VideoMetadata ExtractMetadata(DoorbotHistoryEvent ringEvent)
        {
            if (ringEvent == null)
            {
                throw new ArgumentNullException(nameof(ringEvent));
            }

            var metadata = new VideoMetadata
            {
                EventDateTime = ringEvent.CreatedAtDateTime,
                RingEventKind = ringEvent.Kind,
                RingEventId = ringEvent.Id
            };

            ExtractDeviceInfo(ringEvent.Doorbot, metadata);
            ExtractLocationInfo(ringEvent.Doorbot, metadata);
            ExtractCvProperties(ringEvent.CvProperties, metadata);
            DetermineEventType(ringEvent, metadata);
            BuildKeywords(metadata);
            BuildComment(metadata);

            return metadata;
        }

        private void ExtractDeviceInfo(Doorbot? device, VideoMetadata metadata)
        {
            if (device == null)
            {
                return;
            }

            metadata.DeviceName = device.Description ?? device.Kind;
            metadata.Timezone = device.TimeZone;
            metadata.DeviceManufacturer = "Amazon"; // Ring devices are manufactured by Amazon
            metadata.DeviceModel = DetermineDeviceModel(device.Kind, device.Type);

            if (device.Health != null)
            {
                metadata.Rssi = device.Health.Rssi;
                metadata.BatteryPercentage = device.Health.BatteryPercentage;
            }
        }

        private string? DetermineDeviceModel(string? kind, string? type)
        {
            // Use type if available, otherwise infer from kind
            if (!string.IsNullOrWhiteSpace(type))
            {
                return type;
            }

            if (string.IsNullOrWhiteSpace(kind))
            {
                return null;
            }

            // Map Ring kinds to common device models
            return kind.ToLowerInvariant() switch
            {
                "doorbot" => "Doorbell",
                "stickupcam" or "stickup" => "Stick Up Cam",
                "stickupcam_pro" => "Stick Up Cam Pro",
                "indoor_camera" => "Indoor Cam",
                "outdoor_camera" => "Outdoor Cam",
                "floodlight_camera" => "Floodlight Cam",
                "chime" => "Chime",
                "beams_lightgroup_v3" => "Smart Lighting",
                _ => kind
            };
        }

        private void ExtractLocationInfo(Doorbot? device, VideoMetadata metadata)
        {
            if (device == null)
            {
                return;
            }

            if (device.Latitude.HasValue && device.Longitude.HasValue)
            {
                metadata.Latitude = device.Latitude;
                metadata.Longitude = device.Longitude;
            }

            if (!string.IsNullOrWhiteSpace(device.Address))
            {
                metadata.Address = device.Address;
            }
        }

        private void ExtractCvProperties(CvProperties? cvProperties, VideoMetadata metadata)
        {
            if (cvProperties == null)
            {
                metadata.MotionDetected = true; // Assume motion if event exists
                return;
            }

            if (cvProperties.PersonDetected.HasValue)
            {
                metadata.PersonDetected = cvProperties.PersonDetected.Value;
            }

            if (!string.IsNullOrWhiteSpace(cvProperties.DetectionType))
            {
                metadata.DetectionType = cvProperties.DetectionType;
                // Motion is implied if detection type is present
                metadata.MotionDetected = true;
            }
            else if (cvProperties.PersonDetected.HasValue && cvProperties.PersonDetected.Value)
            {
                metadata.MotionDetected = true;
            }

            if (cvProperties.Similarity.HasValue)
            {
                metadata.DetectionConfidence = cvProperties.Similarity.Value;
            }
        }

        private void DetermineEventType(DoorbotHistoryEvent ringEvent, VideoMetadata metadata)
        {
            // PhotoPrism event types based on Ring detection
            if (metadata.PersonDetected == true)
            {
                metadata.EventType = EventTypePerson;
            }
            else if (string.Equals(ringEvent.Kind, "motion", StringComparison.OrdinalIgnoreCase))
            {
                metadata.EventType = EventTypeMotion;
            }
            else if (string.Equals(ringEvent.Kind, "doorbell", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ringEvent.Kind, "button", StringComparison.OrdinalIgnoreCase))
            {
                metadata.EventType = EventTypeDoorbell;
            }
            else
            {
                metadata.EventType = EventTypeRing;
            }
        }

        private void BuildKeywords(VideoMetadata metadata)
        {
            var keywords = new List<string>();

            if (!string.IsNullOrWhiteSpace(metadata.EventType))
            {
                keywords.Add(metadata.EventType);
            }

            if (!string.IsNullOrWhiteSpace(metadata.DetectionType))
            {
                keywords.Add(metadata.DetectionType.ToLowerInvariant());
            }

            if (metadata.PersonDetected == true)
            {
                keywords.Add("person");
            }

            if (metadata.MotionDetected == true && !keywords.Contains("motion"))
            {
                keywords.Add("motion");
            }

            if (!string.IsNullOrWhiteSpace(metadata.DeviceName))
            {
                keywords.Add(NormalizeKeyword(metadata.DeviceName));
            }

            if (!string.IsNullOrWhiteSpace(metadata.RingEventKind))
            {
                keywords.Add(metadata.RingEventKind.ToLowerInvariant());
            }

            if (keywords.Any())
            {
                metadata.Keywords = keywords.Distinct().ToList();
            }
        }

        private void BuildComment(VideoMetadata metadata)
        {
            var parts = new List<string>();

            if (metadata.EventDateTime.HasValue)
            {
                parts.Add($"Event: {metadata.EventDateTime:g}");
            }

            if (!string.IsNullOrWhiteSpace(metadata.DeviceName))
            {
                parts.Add($"Camera: {metadata.DeviceName}");
            }

            if (metadata.PersonDetected == true)
            {
                parts.Add("Person detected");
            }

            if (metadata.MotionDetected == true && !string.IsNullOrWhiteSpace(metadata.DetectionType))
            {
                parts.Add($"Detection: {metadata.DetectionType}");
            }

            if (metadata.BatteryPercentage.HasValue)
            {
                parts.Add($"Battery: {metadata.BatteryPercentage}%");
            }

            if (metadata.Rssi.HasValue)
            {
                parts.Add($"Signal: {metadata.Rssi:F1} dBm");
            }

            if (parts.Any())
            {
                metadata.Comment = string.Join(" | ", parts);
            }
        }

        private string NormalizeKeyword(string keyword)
        {
            return System.Text.RegularExpressions.Regex.Replace(keyword, @"\s+", "-")
                .ToLowerInvariant()
                .Replace("_", "-");
        }
    }
}
