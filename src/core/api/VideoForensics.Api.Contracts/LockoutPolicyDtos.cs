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
    public record LockoutPolicySettingsDto(
        int MaxFailedAttempts,
        int LockoutDurationMinutes,
        string? BlockedCountryCodes,
        bool FailClosedOnLookupError,
        DateTime UpdatedAtUtc
    );

    /// <summary>
    /// Data transfer object for a request to update lockout policy settings.
    /// </summary>
    /// <param name="MaxFailedAttempts">Maximum number of consecutive failed login attempts before account lockout is triggered.</param>
    /// <param name="LockoutDurationMinutes">Duration in minutes that an account remains locked after exceeding MaxFailedAttempts.</param>
    /// <param name="BlockedCountryCodes">Comma-separated list of ISO 3166-1 alpha-2 country codes to block from login. Null or empty means no country-based blocking is enforced.</param>
    /// <param name="FailClosedOnLookupError">When true, fail closed (block login) if a GeoIP or threat-intelligence lookup fails. When false, fail open (allow login) on lookup failure.</param>
    public record UpdateLockoutPolicySettingsRequest(
        int MaxFailedAttempts,
        int LockoutDurationMinutes,
        string? BlockedCountryCodes,
        bool FailClosedOnLookupError
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
                UpdatedAtUtc: settings.UpdatedAtUtc
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
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = operatorId
            };
        }
    }
}
