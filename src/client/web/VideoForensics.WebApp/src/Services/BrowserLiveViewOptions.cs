namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Configuration for the browser-side live view WebRTC leg. Bound from the
    /// <see cref="SectionName"/> configuration section.
    /// </summary>
    public sealed class BrowserLiveViewOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "BrowserLiveView";

        /// <summary>
        /// STUN/TURN URLs given to the browser-leg peer connection for ICE gathering.
        /// </summary>
        public string[] IceServerUrls { get; set; } = ["stun:stun.l.google.com:19302"];

        /// <summary>
        /// Upper bound on the SDP offer creation, including ICE gathering. When an ICE server is
        /// unreachable, gathering can block far longer than this. On timeout the browser leg is
        /// disposed and the caller receives an error instead of waiting.
        /// </summary>
        public TimeSpan OfferGatheringTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}
