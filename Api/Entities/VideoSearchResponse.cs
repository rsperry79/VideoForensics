using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/video_search/history - an alternate,
    /// date-range-filterable search over the same underlying events as GetDoorbotsHistory(). Field
    /// shape mirrors ring-client-api's videoSearch() but is not confirmed against a live capture.
    /// </summary>
    public class VideoSearchResponse
    {
        [JsonPropertyName("video_search")]
        public List<DoorbotHistoryEvent> VideoSearch { get; set; }
    }
}
