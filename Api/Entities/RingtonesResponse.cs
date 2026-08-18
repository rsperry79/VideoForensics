using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/ringtones. Confirmed via a live
    /// ApiTester run: the list is under "audios", not "ringtones" as originally assumed - the
    /// original mapping meant GetRingtones() silently always returned an empty list.
    /// </summary>
    public class RingtonesResponse
    {
        [JsonPropertyName("audios")]
        public List<Ringtone> Ringtones { get; set; }
    }
}
