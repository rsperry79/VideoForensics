using System.Threading.RateLimiting;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.RateLimiting
{
    /// <summary>
    /// Builds the per-request partition for the "media" rate-limit policy (plan §5.7), covering
    /// /api/v1/media/* so an already-paired device can't hammer it into a self-inflicted DoS.
    /// </summary>
    public static class MediaRateLimitPolicy
    {
        /// <summary>
        /// Network and Internet requests allowed per minute per client. Sized for a full camera grid: 30 requests per tile
        /// per minute across <see cref="VideoForensics.Client.Common.Viewer.CameraSync.MaxTiles"/> tiles, so a grid scrub
        /// does not hit 429 while a single client still cannot flood the server.
        /// </summary>
        public const int PermitsPerMinute = 30 * VideoForensics.Client.Common.Viewer.CameraSync.MaxTiles;

        /// <summary>
        /// Returns an unlimited partition for Local-tier requests, otherwise a 120-requests-per-minute
        /// sliding window keyed by the resolved client IP.
        /// </summary>
        /// <remarks>
        /// Local is exempt because the single owner is physically at the loopback machine, and the
        /// thumbnail page legitimately fires hundreds of media requests at once - applying the limit
        /// there would self-throttle the owner's own browser (429s) without protecting against anyone.
        /// Network and Internet keep the limit, since that is the traffic a flood would come from.
        /// </remarks>
        /// <param name="httpContext">The request being partitioned; used to resolve its tier and client IP.</param>
        /// <param name="resolver">Resolves the request's network tier and real client IP.</param>
        /// <returns>A partition keyed by the client IP, either unlimited or sliding-window limited.</returns>
        public static RateLimitPartition<string> Create(HttpContext httpContext, INetworkTierResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(resolver);

            string key = resolver.ResolveClientIp(httpContext);

            return resolver.ResolveTier(httpContext) == NetworkTier.Local
                ? RateLimitPartition.GetNoLimiter(key)
                : RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0
                });
        }
    }
}
