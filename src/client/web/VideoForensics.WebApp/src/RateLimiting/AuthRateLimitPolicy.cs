using System.Threading.RateLimiting;

using VideoForensics.Client.Hosting;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.RateLimiting
{
    /// <summary>
    /// Builds the per-request partition for the "auth" rate-limit policy (plan §5.7), covering pairing and sign-in
    /// endpoints so a remote attacker cannot brute-force them.
    /// </summary>
    public static class AuthRateLimitPolicy
    {
        /// <summary>
        /// Requests allowed per <see cref="Window"/> per client on Network and Internet tiers. A passkey sign-in is two
        /// requests (options, then complete), so this must cover several sign-ins and the occasional retry.
        /// </summary>
        public const int PermitLimit = 20;

        /// <summary>Length of the sliding window for <see cref="PermitLimit"/>.</summary>
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Returns an unlimited partition for Local-tier requests, otherwise a sliding window of
        /// <see cref="PermitLimit"/> requests per <see cref="Window"/>, keyed by the resolved client IP.
        /// </summary>
        /// <param name="httpContext">The request being partitioned; used to resolve its tier and client IP.</param>
        /// <param name="resolver">Resolves the request's network tier and real client IP.</param>
        public static RateLimitPartition<string> Create(HttpContext httpContext, INetworkTierResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(resolver);

            string key = resolver.ResolveClientIp(httpContext);

            // Local is the owner at the loopback machine, so it is not limited. Network and Internet keep the limit.
            return resolver.ResolveTier(httpContext) == NetworkTier.Local
                ? RateLimitPartition.GetNoLimiter(key)
                : RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    SegmentsPerWindow = 3,
                    QueueLimit = 0
                });
        }
    }
}
