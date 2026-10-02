using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Implementation of <see cref="IAuthMethodSettingsService"/> - manages auth method settings
    /// (password and passkey enablement) persisted via <see cref="IAppSettingRepository"/>.
    /// </summary>
    public class AuthMethodSettingsService : IAuthMethodSettingsService
    {
        private readonly IAppSettingRepository _settings;

        // Setting keys
        private const string PasswordEnabledKey = "AuthPasswordEnabled";
        private const string PasskeyEnabledKey = "AuthPasskeyEnabled";
        private const string UpdatedAtUtcKey = "AuthMethodsUpdatedAtUtc";

        public AuthMethodSettingsService(IAppSettingRepository settings)
        {
            _settings = settings;
        }

        public async Task<AuthMethodSettingsDto> GetAsync(CancellationToken ct)
        {
            // Retrieve settings with defaults (both true by default)
            string? passwordEnabledStr = await _settings.GetAsync(PasswordEnabledKey, ct);
            string? passkeyEnabledStr = await _settings.GetAsync(PasskeyEnabledKey, ct);

            bool passwordEnabled = string.IsNullOrEmpty(passwordEnabledStr) || bool.Parse(passwordEnabledStr);
            bool passkeyEnabled = string.IsNullOrEmpty(passkeyEnabledStr) || bool.Parse(passkeyEnabledStr);

            return new AuthMethodSettingsDto(
                PasswordEnabled: passwordEnabled,
                PasskeyEnabled: passkeyEnabled);
        }

        /// <summary>
        /// Get the UTC timestamp of the last update to auth method settings.
        /// Returns DateTime.MinValue if settings have never been updated.
        /// </summary>
        public async Task<DateTime> GetLastUpdatedAtUtcAsync(CancellationToken ct)
        {
            string? updatedAtStr = await _settings.GetAsync(UpdatedAtUtcKey, ct);
            if (string.IsNullOrEmpty(updatedAtStr))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParseExact(updatedAtStr, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime result))
            {
                return result;
            }

            return DateTime.MinValue;
        }

        public async Task<bool> IsEnabledAsync(string methodName, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(methodName);

            return methodName.ToLowerInvariant() switch
            {
                "password" =>
                    await _settings.GetAsync(PasswordEnabledKey, ct) is var s &&
                    (string.IsNullOrEmpty(s) || bool.Parse(s)),

                "passkey" =>
                    await _settings.GetAsync(PasskeyEnabledKey, ct) is var s &&
                    (string.IsNullOrEmpty(s) || bool.Parse(s)),

                _ => false
            };
        }

        public async Task<bool> UpdateAsync(UpdateAuthMethodSettingsRequest request, CancellationToken ct)
        {
            // Get current settings to calculate new state
            var current = await GetAsync(ct);

            // Determine new state for validation
            bool newPasswordEnabled = request.PasswordEnabled ?? current.PasswordEnabled;
            bool newPasskeyEnabled = request.PasskeyEnabled ?? current.PasskeyEnabled;

            // Validate: at least one of password or passkey must remain enabled
            if (!newPasswordEnabled && !newPasskeyEnabled)
            {
                return false;
            }

            // Apply updates
            if (request.PasswordEnabled.HasValue)
            {
                await _settings.SetAsync(PasswordEnabledKey, request.PasswordEnabled.Value.ToString(), ct);
            }

            if (request.PasskeyEnabled.HasValue)
            {
                await _settings.SetAsync(PasskeyEnabledKey, request.PasskeyEnabled.Value.ToString(), ct);
            }

            // Persist the timestamp of this update
            await _settings.SetAsync(UpdatedAtUtcKey, DateTime.UtcNow.ToString("O"), ct);

            return true;
        }
    }
}
