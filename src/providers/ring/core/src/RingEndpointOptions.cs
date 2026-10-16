using System;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Base URIs for the Ring APIs used by <see cref="Session"/>. The defaults are the production Ring hosts;
    /// override individual properties to point a session at a different host (for example a test server).
    /// </summary>
    /// <remarks>
    /// Host map. Ring splits its APIs across two hosts. This is not documented by Ring; it comes from
    /// dgreif/ring ring-client-api (rest-client.ts, location.ts, ring-intercom.ts) and from live probes, 2026-10-16:
    /// <list type="bullet">
    /// <item><description><c>prd-api-us.prd.rings.solutions/api/v1/</c>: app API. Location mode (<c>mode/location/...</c>),
    /// monitoring (<c>rs/monitoring/accounts/...</c>), and signalling tickets (<c>clap/tickets</c>). Probed: mode 200,
    /// monitoring 200, signalling 400 "No authorized devices" on a camera-only location. The same path on
    /// <c>api.ring.com/api/v1/</c> returns 404.</description></item>
    /// <item><description><c>api.ring.com/clients_api/</c>, <c>api.ring.com/devices/v1/</c>, <c>api.ring.com/commands/v1/</c>,
    /// <c>api.ring.com/groups/v1/</c>, <c>api.ring.com/integrations/</c>, <c>api.ring.com/recordings/public/</c>,
    /// <c>api.ring.com/evm/v2/history/locations/</c>: stays on api.ring.com per ring-client-api. Probed: the clients API
    /// paths <c>ring_devices</c> and <c>profile</c> return 404 on the app API host. The other paths are not probed on it.</description></item>
    /// <item><description><c>app-snaps.ring.com</c>: snapshot host, separate from both (see Session.SnapshotsExtra.cs).</description></item>
    /// </list>
    /// Sources: https://github.com/dgreif/ring/blob/main/packages/ring-client-api/rest-client.ts and
    /// https://github.com/dgreif/ring/blob/main/packages/ring-client-api/location.ts
    /// </remarks>
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
