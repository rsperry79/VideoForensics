using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/ringtones
    /// </summary>
    public class RingtonesResponse
    {
        [JsonPropertyName("ringtones")]
        public List<Ringtone> Ringtones { get; set; }
    }
}
