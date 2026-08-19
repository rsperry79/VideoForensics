using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/ringtones. Confirmed via a live
    /// ApiTester run: the list is under "audios", not "ringtones" as originally assumed - the
    /// original mapping meant GetRingtones() silently always returned an empty list.
    /// Includes default ringtone assignments for different event types.
    /// </summary>
    public class RingtonesResponse
    {
        [JsonPropertyName("audios")]
        public List<Ringtone> Ringtones { get; set; }

        [JsonPropertyName("default_ding_id")]
        public string DefaultDingId { get; set; }

        [JsonPropertyName("default_ding_user_id")]
        public string DefaultDingUserId { get; set; }

        [JsonPropertyName("default_motion_id")]
        public string DefaultMotionId { get; set; }

        [JsonPropertyName("default_motion_user_id")]
        public string DefaultMotionUserId { get; set; }

        [JsonPropertyName("default_alarm_user_id")]
        public string DefaultAlarmUserId { get; set; }

        [JsonPropertyName("default_chirp_user_id")]
        public string DefaultChirpUserId { get; set; }

        [JsonPropertyName("default_appstore_user_id")]
        public string DefaultAppstoreUserId { get; set; }
    }
}
