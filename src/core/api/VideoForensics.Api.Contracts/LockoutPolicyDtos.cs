using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for lockout policy settings: maximum failed login attempts, lockout duration,
    /// GeoIP-based country blocking, and lookup failure behavior.
    /// </summary>
    /// <param name="MaxFailedAttempts">Maximum number of consecutive failed login attempts before account lockout is triggered.</param>
    /// <param name="LockoutDurationMinutes">Duration in minutes that an account remains locked after exceeding MaxFailedAttempts.</param>
    /// <param name="BlockedCountryCodes">Comma-separated list of ISO 3166-1 alpha-2 country codes to block from login (e.g. "KP,IR"). Null or empty means no country-based blocking is enforced.</param>
    /// <param name="FailClosedOnLookupError">When true, fail closed (block login) if a GeoIP or threat-intelligence lookup fails. When false, fail open (allow login) on lookup failure.</param>
    /// <param name="UpdatedAtUtc">Timestamp of the last update to these settings, in UTC.</param>
    /// <param name="AutoBanEnabled">When true, client IPs that trigger lockouts across enough distinct accounts are automatically banned.</param>
    /// <param name="AutoBanDistinctAccountThreshold">Distinct locked-out accounts from one client IP that trigger an auto-ban. Must be at least 1.</param>
    /// <param name="AutoBanWindowMinutes">Window in minutes over which distinct-account lockouts are counted. Must be at least 1.</param>
    /// <param name="AutoBanDurationMinutes">Duration in minutes of an auto-created IP ban. Must be at least 1.</param>
    public record LockoutPolicySettingsDto(
        int MaxFailedAttempts,
        int LockoutDurationMinutes,
        string? BlockedCountryCodes,
        bool FailClosedOnLookupError,
        DateTime UpdatedAtUtc,
        bool AutoBanEnabled = false,
        int AutoBanDistinctAccountThreshold = 3,
        int AutoBanWindowMinutes = 60,
        int AutoBanDurationMinutes = 1440
    );

    /// <summary>
    /// Data transfer object for a request to update lockout policy settings.
    /// </summary>
    /// <param name="MaxFailedAttempts">Maximum number of consecutive failed login attempts before account lockout is triggered.</param>
    /// <param name="LockoutDurationMinutes">Duration in minutes that an account remains locked after exceeding MaxFailedAttempts.</param>
    /// <param name="BlockedCountryCodes">Comma-separated list of ISO 3166-1 alpha-2 country codes to block from login. Null or empty means no country-based blocking is enforced.</param>
    /// <param name="FailClosedOnLookupError">When true, fail closed (block login) if a GeoIP or threat-intelligence lookup fails. When false, fail open (allow login) on lookup failure.</param>
    /// <param name="AutoBanEnabled">When true, client IPs that trigger lockouts across enough distinct accounts are automatically banned.</param>
    /// <param name="AutoBanDistinctAccountThreshold">Distinct locked-out accounts from one client IP that trigger an auto-ban. Must be at least 1.</param>
    /// <param name="AutoBanWindowMinutes">Window in minutes over which distinct-account lockouts are counted. Must be at least 1.</param>
    /// <param name="AutoBanDurationMinutes">Duration in minutes of an auto-created IP ban. Must be at least 1.</param>
    public record UpdateLockoutPolicySettingsRequest(
        int MaxFailedAttempts,
        int LockoutDurationMinutes,
        string? BlockedCountryCodes,
        bool FailClosedOnLookupError,
        bool AutoBanEnabled = false,
        int AutoBanDistinctAccountThreshold = 3,
        int AutoBanWindowMinutes = 60,
        int AutoBanDurationMinutes = 1440
    );

    /// <summary>Extension methods for mapping lockout policy domain types to/from DTOs.</summary>
    public static class LockoutPolicySettingsDtoMapping
    {
        /// <summary>
        /// Converts a domain LockoutPolicySettings to a LockoutPolicySettingsDto.
        /// </summary>
        public static LockoutPolicySettingsDto ToDto(this LockoutPolicySettings settings)
        {
            return new LockoutPolicySettingsDto(
                MaxFailedAttempts: settings.MaxFailedAttempts,
                LockoutDurationMinutes: settings.LockoutDurationMinutes,
                BlockedCountryCodes: settings.BlockedCountryCodes,
                FailClosedOnLookupError: settings.FailClosedOnLookupError,
                UpdatedAtUtc: settings.UpdatedAtUtc,
                AutoBanEnabled: settings.AutoBanEnabled,
                AutoBanDistinctAccountThreshold: settings.AutoBanDistinctAccountThreshold,
                AutoBanWindowMinutes: settings.AutoBanWindowMinutes,
                AutoBanDurationMinutes: settings.AutoBanDurationMinutes
            );
        }

        /// <summary>
        /// Converts a LockoutPolicySettingsDto to a domain LockoutPolicySettings.
        /// </summary>
        public static LockoutPolicySettings ToDomain(this LockoutPolicySettingsDto dto, Guid? operatorId = null)
        {
            return new LockoutPolicySettings
            {
                Id = Guid.NewGuid(), // Use a default ID; caller should set the singleton ID
                MaxFailedAttempts = dto.MaxFailedAttempts,
                LockoutDurationMinutes = dto.LockoutDurationMinutes,
                BlockedCountryCodes = dto.BlockedCountryCodes,
                FailClosedOnLookupError = dto.FailClosedOnLookupError,
                AutoBanEnabled = dto.AutoBanEnabled,
                AutoBanDistinctAccountThreshold = dto.AutoBanDistinctAccountThreshold,
                AutoBanWindowMinutes = dto.AutoBanWindowMinutes,
                AutoBanDurationMinutes = dto.AutoBanDurationMinutes,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = operatorId
            };
        }
    }
}
