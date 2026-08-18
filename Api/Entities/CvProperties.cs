using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// A single classified detection within a CV-evaluated event, with the timestamps (epoch ms)
    /// it was confirmed at. Confirmed via a live ApiTester run of video_search/history and
    /// locations/{id}/events.
    /// </summary>
    public class CvDetectionType
    {
        [JsonPropertyName("detection_type")]
        public string DetectionType { get; set; }

        [JsonPropertyName("verified_timestamps")]
        public List<long> VerifiedTimestamps { get; set; }
    }

    /// <summary>
    /// Computer vision properties returned by the Ring API for a history event.
    /// Indicates whether the Ring backend detected a person (or other classified object) in the recording.
    /// </summary>
    public class CvProperties
    {
        /// <summary>
        /// When true, Ring's computer vision detected a person in the recording.
        /// May be null if the event was not evaluated by CV (e.g. rings, older firmware, etc.).
        /// </summary>
        [JsonPropertyName("person_detected")]
        public bool? PersonDetected { get; set; }

        /// <summary>
        /// When true, Ring indicated the recording stream was broken / incomplete.
        /// </summary>
        [JsonPropertyName("stream_broken")]
        public bool? StreamBroken { get; set; }

        /// <summary>
        /// The classified detection type reported by Ring (e.g. "human", "vehicle", "animal", "other_motion").
        /// </summary>
        [JsonPropertyName("detection_type")]
        public string DetectionType { get; set; }

        /// <summary>
        /// The individual classified detections (each with its own confirmation timestamps) that
        /// make up this event's overall <see cref="DetectionType"/>.
        /// </summary>
        [JsonPropertyName("detection_types")]
        public List<CvDetectionType> DetectionTypes { get; set; }
    }
}
