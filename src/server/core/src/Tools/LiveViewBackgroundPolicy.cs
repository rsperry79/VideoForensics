namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// Rules for background live-view sessions: a session promoted to sustained mode can run for a duration the caller
    /// asks for, capped by the configured safety limit. Kept pure so the rules can be tested without the orchestrator.
    /// </summary>
    public static class LiveViewBackgroundPolicy
    {
        /// <summary>
        /// The duration a background session runs for: the requested duration, but never more than the cap.
        /// No request means the full cap.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The requested duration is zero or negative.</exception>
        public static TimeSpan Clamp(TimeSpan? requested, TimeSpan cap)
        {
            if (requested is null)
            {
                return cap;
            }

            if (requested.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(requested), requested, "A background duration must be positive.");
            }

            return requested.Value < cap ? requested.Value : cap;
        }

        /// <summary>True once <paramref name="nowUtc"/> reaches the deadline. No deadline never expires.</summary>
        public static bool IsExpired(DateTime? deadlineUtc, DateTime nowUtc)
        {
            return deadlineUtc.HasValue && nowUtc >= deadlineUtc.Value;
        }

        /// <summary>The UTC moment a background session started at <paramref name="sinceUtc"/> ends after <paramref name="duration"/>.</summary>
        public static DateTime DeadlineFor(DateTime sinceUtc, TimeSpan duration)
        {
            return DateTime.SpecifyKind(sinceUtc.Add(duration), DateTimeKind.Utc);
        }
    }
}
