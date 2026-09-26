using Microsoft.Extensions.Primitives;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Auth
{
    /// <summary>
    /// Shared "which network tier does the WebApp's own self-HTTP <c>X-VF-Session-Tier</c> header
    /// actually let this request claim" rule, used both by an ALREADY-authenticated request's tier
    /// claim (<see cref="PairedDeviceAuthenticationHandler"/>) and by a PRE-AUTH endpoint's own
    /// tier-gated decision (e.g. <c>OperatorAuthEndpoints.LoginPasswordAsync</c>'s primary-SuperAdmin
    /// Local-only check). Both need it because BOTH kinds of self-HTTP call the WebApp's own Blazor UI
    /// makes back into its own Minimal API travel over loopback regardless of where the real browser
    /// physically is (see <c>SelfHttpServiceExtensions</c>' and <c>SessionTierHeaderHandler</c>'s doc
    /// comments) - so neither can simply resolve tier from the self-call's own connection.
    ///
    /// Rules, in order:
    /// - No header at all: unchanged behavior - resolve from the connection (loopback tools/MAUI on
    ///   the same machine legitimately have no such header).
    /// - Header present but the connection ISN'T loopback: a genuinely remote caller cannot use this
    ///   header to claim a better tier than their real one - ignore it entirely and resolve by IP.
    /// - Header present on a loopback connection: unprotect it and, if it decrypts, is unexpired, AND
    ///   its (possibly null, for a pre-auth payload) operator id is acceptable for this kind of
    ///   request, use its tier - otherwise fail safe to Internet (the most restrictive tier) rather
    ///   than silently trusting the loopback connection.
    /// </summary>
    public static class RequestTierResolver
    {
        /// <summary>
        /// For an ALREADY-authenticated request: the header must be bound to THIS operator - a
        /// pre-auth header (no operator bound at all, see <see cref="ISessionTierHeaderProtector.ProtectPreAuth"/>)
        /// on an authenticated request is rejected, same as one bound to a different operator, since a
        /// stale/replayed pre-auth header must never inherit an authenticated caller's Local-tier
        /// trust.
        /// </summary>
        public static NetworkTier ResolveForOperator(
            HttpContext context,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILogger logger,
            Guid operatorId)
        {
            return Resolve(context, tierResolver, headerProtector, logger, headerOperatorId => headerOperatorId == operatorId);
        }

        /// <summary>
        /// For a PRE-AUTH endpoint (no session/operator identity exists yet - e.g. password login,
        /// first-run setup, device pairing/registration): both a pre-auth header and an operator-bound
        /// one (stronger evidence than pre-auth) are accepted, since there is no operator identity yet
        /// to bind a pre-auth decision to.
        /// </summary>
        public static NetworkTier ResolvePreAuth(
            HttpContext context,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILogger logger)
        {
            return Resolve(context, tierResolver, headerProtector, logger, _ => true);
        }

        private static NetworkTier Resolve(
            HttpContext context,
            INetworkTierResolver tierResolver,
            ISessionTierHeaderProtector headerProtector,
            ILogger logger,
            Func<Guid?, bool> isAcceptableOperatorId)
        {
            NetworkTier connectionTier = tierResolver.ResolveTier(context);

            if (!context.Request.Headers.TryGetValue(SessionTierHeaderNames.HeaderName, out StringValues headerValues))
            {
                return connectionTier;
            }

            if (connectionTier != NetworkTier.Local)
            {
                logger.LogWarning(
                    "Session-tier header present on a non-loopback request (resolved tier {ConnectionTier}); ignoring it and resolving by IP.",
                    connectionTier);
                return connectionTier;
            }

            string headerValue = headerValues.ToString();
            if (headerProtector.TryUnprotect(headerValue, out NetworkTier headerTier, out Guid? headerOperatorId) && isAcceptableOperatorId(headerOperatorId))
            {
                return headerTier;
            }

            logger.LogWarning(
                "Session-tier header present on a loopback request but failed validation (expired, tampered, operator mismatch, or pre-auth on an authenticated request); failing safe to Internet tier.");
            return NetworkTier.Internet;
        }
    }
}
