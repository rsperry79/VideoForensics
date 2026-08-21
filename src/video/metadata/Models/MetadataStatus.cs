using System.Text.Json.Serialization;

namespace Ring.Api.Video.Metadata.Models
{
    /// <summary>
    /// Indicates the validation and correction status of video metadata.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MetadataStatus
    {
        /// <summary>
        /// Metadata was not processed (skipped or unavailable).
        /// </summary>
        NotProcessed = 0,

        /// <summary>
        /// Metadata was successfully written and the file is valid.
        /// </summary>
        Valid = 1,

        /// <summary>
        /// Metadata was written and some issues were corrected automatically.
        /// </summary>
        Corrected = 2,

        /// <summary>
        /// Metadata could not be written or validated; file remains unchanged.
        /// The video is likely still playable but lacks proper metadata.
        /// </summary>
        Corrupt = 3,

        /// <summary>
        /// Metadata processing failed completely; check error message.
        /// </summary>
        Failed = 4
    }
}
