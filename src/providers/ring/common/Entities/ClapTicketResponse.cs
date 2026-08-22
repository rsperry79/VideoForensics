using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Response shape of GET api/v1/clap/tickets?locationID={id}&amp;requestedTransport=ws - grants a
    /// short-lived ticket to open an authenticated device-command websocket ("asset socket") for a
    /// location, used for Ring Alarm control. Distinct from the clap/ticket/request/signalsocket
    /// endpoint used for WebRTC live view signaling (see ClapSignalingTicketResponse.cs).
    /// </summary>
    public class ClapTicketResponse
    {
        [JsonPropertyName("assets")]
        public List<string> Assets { get; set; }

        [JsonPropertyName("ticket")]
        public string Ticket { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; }
    }
}
