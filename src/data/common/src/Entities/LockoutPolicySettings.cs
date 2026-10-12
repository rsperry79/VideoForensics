namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Singleton account lockout configuration: maximum failed login attempts, lockout duration, GeoIP-based country blocking, and lookup failure behavior.
    /// There is exactly one row in this table, keyed by Id.
    /// </summary>
    public class LockoutPolicySettings
    {
        /// <summary>
        /// Unique identifier for this singleton settings row (always the same Guid across all instances).
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Maximum number of consecutive failed login attempts (password or LDAP) before account lockout is triggered.
        /// </summary>
        public int MaxFailedAttempts { get; set; } = 5;

        /// <summary>
        /// Duration in minutes that an account remains locked after exceeding MaxFailedAttempts.
        /// </summary>
        public int LockoutDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Comma-separated list of ISO 3166-1 alpha-2 country codes to block from login (e.g. "KP,IR").
        /// Null or empty means no country-based blocking is enforced.
        /// </summary>
        public string? BlockedCountryCodes { get; set; }

        /// <summary>
        /// When true, fail closed (block login) if a GeoIP or threat-intelligence lookup fails.
        /// When false, fail open (allow login) on lookup failure, preventing a broken geo-service from locking everyone out.
        /// Default false (fail open) prioritizes availability.
        /// </summary>
        public bool FailClosedOnLookupError { get; set; }

        /// <summary>
        /// When true, an IP that triggers lockouts across enough distinct accounts within the window is automatically
        /// banned (fail2ban-style). Default false: the feature is opt-in until its false-positive rate is understood.
        /// </summary>
        public bool AutoBanEnabled { get; set; } = false;

        /// <summary>
        /// Number of distinct accounts that must be locked out from one client IP within <see cref="AutoBanWindowMinutes"/>
        /// before that IP is auto-banned. Must be at least 1.
        /// </summary>
        public int AutoBanDistinctAccountThreshold { get; set; } = 3;

        /// <summary>
        /// Sliding window in minutes over which distinct-account lockouts from one client IP are counted. Must be at least 1.
        /// </summary>
        public int AutoBanWindowMinutes { get; set; } = 60;

        /// <summary>
        /// Duration in minutes that an auto-created IP ban remains in effect. Must be at least 1.
        /// Default 1440 (24 hours).
        /// </summary>
        public int AutoBanDurationMinutes { get; set; } = 1440;

        /// <summary>
        /// Timestamp of the last update to these settings, in UTC.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Id of the Operator who last updated these settings; null if updated by system initialization.
        /// </summary>
        public Guid? UpdatedByOperatorId { get; set; }
    }
}
