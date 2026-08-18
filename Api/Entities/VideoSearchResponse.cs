using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/video_search/history - a date-range
    /// search over recorded events that, unlike GetDoorbotsHistory(), returns direct (temporary,
    /// pre-signed) download URLs inline rather than requiring the separate dings/{id}/share/download
    /// polling GetDoorbotHistoryRecording() does. Confirmed against a live account via ApiTester.
    /// </summary>
    public class VideoSearchResponse
    {
        [JsonPropertyName("video_search")]
        public List<VideoSearchItem> VideoSearch { get; set; }
    }

    /// <summary>
    /// A single recorded event returned by video_search/history. Field shape confirmed against a
    /// live account via ApiTester - notably created_at/updated_at are epoch milliseconds here,
    /// unlike the ISO-8601 string GetDoorbotsHistory() returns for the same concept.
    /// </summary>
    public class VideoSearchItem
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Id { get; set; }

        [JsonPropertyName("ding_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DingId { get; set; }

        [JsonPropertyName("created_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? CreatedAtUnixMs { get; set; }

        /// <summary>UTC date/time this event was recorded, converted from <see cref="CreatedAtUnixMs"/>.</summary>
        public DateTime? CreatedAt => CreatedAtUnixMs.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtUnixMs.Value).UtcDateTime
            : null;

        [JsonPropertyName("updated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? UpdatedAtUnixMs { get; set; }

        [JsonPropertyName("kind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Kind { get; set; }

        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string State { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Duration { get; set; }

        [JsonPropertyName("favorite")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Favorite { get; set; }

        /// <summary>Temporary, pre-signed high-quality download URL - expires; download promptly.</summary>
        [JsonPropertyName("hq_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string HqUrl { get; set; }

        /// <summary>Temporary, pre-signed low-quality download URL - expires; download promptly.</summary>
        [JsonPropertyName("lq_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LqUrl { get; set; }

        [JsonPropertyName("thumbnail_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ThumbnailUrl { get; set; }

        [JsonPropertyName("untranscoded_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UntranscodedUrl { get; set; }

        [JsonPropertyName("doorbot")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Doorbot Doorbot { get; set; }

        [JsonPropertyName("cv_properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CvProperties CvProperties { get; set; }
    }
}
