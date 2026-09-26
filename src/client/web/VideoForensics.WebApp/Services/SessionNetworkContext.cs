using Microsoft.AspNetCore.Http;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Circuit-scoped record of a Blazor Server session's REAL network tier - resolved once, from
    /// the browser's actual initial HTTP connection, and held for the circuit's whole lifetime.
    ///
    /// This exists because a Blazor Server circuit's own HttpContext is only reliably available
    /// during the request that first established the circuit (the prerendering pass) - later,
    /// UI-event-driven code (button clicks, self-HTTP calls, etc.) has no real per-request
    /// connection of its own to resolve a tier from. Any self-HTTP call the WebApp's own UI makes
    /// back into its own Minimal API travels over loopback regardless of where the real browser
    /// physically is, so resolving tier from THAT connection would always (incorrectly) yield
    /// Local. See Components/NetworkTierCapture.razor for how this gets populated (captured during
    /// prerender, carried into the interactive circuit via PersistentComponentState) and
    /// SelfHttpServiceExtensions/SessionTierHeaderHandler for how it is then attached to outgoing
    /// self-HTTP calls so PairedDeviceAuthenticationHandler can recover it server-side.
    /// </summary>
    public class SessionNetworkContext
    {
        private NetworkTier? _tier;

        /// <summary>
        /// This session's real network tier. Internet (the most restrictive tier) until a tier is
        /// captured - an undetermined tier must fail safe to the most restrictive one, never the
        /// most permissive.
        /// </summary>
        public NetworkTier Tier => _tier ?? NetworkTier.Internet;

        /// <summary>When <see cref="Tier"/> was last (successfully) set; null if never set.</summary>
        public DateTime? ResolvedAtUtc { get; private set; }

        /// <summary>
        /// Resolves the tier from the circuit's initial real HTTP connection and records it. A null
        /// <paramref name="httpContext"/> (no connection to resolve from at all) resolves to
        /// Internet, the safe default for "could not be determined".
        /// </summary>
        public void CaptureFromInitialConnection(HttpContext? httpContext, INetworkTierResolver resolver)
        {
            NetworkTier resolved = httpContext != null ? resolver.ResolveTier(httpContext) : NetworkTier.Internet;
            SetTier(resolved);
        }

        /// <summary>
        /// Records a resolved tier for this session. If a tier was already recorded (e.g. this is a
        /// re-resolve on reconnect), only a MORE restrictive tier replaces it - this session's
        /// recorded tier never loosens.
        /// </summary>
        public void SetTier(NetworkTier tier)
        {
            if (_tier is null || tier > _tier.Value)
            {
                _tier = tier;
                ResolvedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
