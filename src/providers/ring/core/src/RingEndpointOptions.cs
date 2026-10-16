using System;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Base URIs for the Ring APIs used by <see cref="Session"/>. The defaults are the production Ring hosts;
    /// override individual properties to point a session at a different host (for example a test server).
    /// </summary>
    public sealed class RingEndpointOptions
    {
        /// <summary>Base Uri for the Ring clients API.</summary>
        public Uri ClientsApiBaseUrl { get; set; } = new("https://api.ring.com/clients_api/");

        /// <summary>Base Uri for the Ring devices API.</summary>
        public Uri DevicesApiBaseUrl { get; set; } = new("https://api.ring.com/devices/v1/");

        /// <summary>Base Uri for the Ring app API (served from the prd-api-us host, which also serves the clap signalling tickets).</summary>
        public Uri AppApiBaseUrl { get; set; } = new("https://prd-api-us.prd.rings.solutions/api/v1/");

        /// <summary>Base Uri for the Ring commands API.</summary>
        public Uri CommandsApiBaseUrl { get; set; } = new("https://api.ring.com/commands/v1/");

        /// <summary>Base Uri for the Ring groups API.</summary>
        public Uri GroupsApiBaseUrl { get; set; } = new("https://api.ring.com/groups/v1/");

        /// <summary>Base Uri for the Ring Location Mode API.</summary>
        public Uri ModeApiBaseUrl { get; set; } = new("https://prd-api-us.prd.rings.solutions/api/v1/mode/");

        /// <summary>Base Uri for the Ring Alarm monitoring accounts API.</summary>
        public Uri MonitoringApiBaseUrl { get; set; } = new("https://prd-api-us.prd.rings.solutions/api/v1/rs/monitoring/accounts/");

        /// <summary>Base Uri for Ring's event-manager history API.</summary>
        public Uri EvmApiBaseUrl { get; set; } = new("https://api.ring.com/evm/v2/history/locations/");

        /// <summary>Base Uri for the Ring integrations API.</summary>
        public Uri IntegrationsApiBaseUrl { get; set; } = new("https://api.ring.com/integrations/");

        /// <summary>Base Uri for the Ring recordings API.</summary>
        public Uri RecordingsApiBaseUrl { get; set; } = new("https://api.ring.com/recordings/public/");

        /// <summary>Base Uri for the Ring app snapshots API.</summary>
        public Uri AppSnapsApiBaseUrl { get; set; } = new("https://app-snaps.ring.com/");
    }
}
