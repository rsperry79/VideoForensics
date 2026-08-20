using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of POST clap/ticket/request/signalsocket - grants a short-lived ticket to open
    /// a WebRTC signaling websocket for live view. Distinct from ClapTicketResponse, which is used
    /// for the Alarm asset socket and returns assets/host as well.
    /// </summary>
    public class ClapSignalingTicketResponse
    {
        [JsonPropertyName("ticket")]
        public string Ticket { get; set; }
    }
}
