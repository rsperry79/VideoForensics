using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
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
