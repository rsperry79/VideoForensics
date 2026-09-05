using System;

namespace VideoForensics.Providers.Ring.Exceptions
{
    /// <summary>
    /// Exception thrown when the Ring API returns HTTP 429 Too many requests
    /// </summary>
    public class ThrottledException : Exception
    {
        /// <summary>
        /// True when this came from the process-wide hard-ban circuit breaker (HttpUtility) rather
        /// than a single fresh 429. A caller retrying on ThrottledException should not retry a hard
        /// ban - every retry will fail identically (no network call is even made) until the ban's
        /// long cooldown elapses, so retrying just wastes time re-running the same short backoff loop
        /// once per device for no chance of success.
        /// </summary>
        public bool IsHardBan { get; }

        /// <summary>The HTTP status code Ring returned, when available (set via object initializer at the throw site, not every caller populates it).</summary>
        public System.Net.HttpStatusCode? StatusCode { get; init; }

        /// <summary>The raw response body Ring returned, truncated to ~4000 chars, when available.</summary>
        public string? ResponseBody { get; init; }

        public ThrottledException() : base("The request has been denied by Ring due to too many requests. Try again in a few minutes.")
        {
        }

        public ThrottledException(Exception innerException) : base("The request has been denied by Ring due to too many requests. Try again in a few minutes.", innerException)
        {
        }

        public ThrottledException(string message, bool isHardBan = false) : base(message)
        {
            IsHardBan = isHardBan;
        }
    }
}
