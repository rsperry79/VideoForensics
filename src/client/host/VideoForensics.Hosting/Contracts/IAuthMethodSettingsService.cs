using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Manages auth method (login method) enablement settings for password and passkey authentication.
    /// Stores all state via <see cref="IAppSettingRepository"/> with keys "AuthPasswordEnabled" and
    /// "AuthPasskeyEnabled" (both default true when absent).
    /// </summary>
    public interface IAuthMethodSettingsService
    {
        /// <summary>
        /// Gets current auth method settings with password and passkey enablement flags.
        /// </summary>
        Task<AuthMethodSettingsDto> GetAsync(CancellationToken ct);

        /// <summary>
        /// Checks whether a specific auth method is enabled.
        /// </summary>
        /// <param name="methodName">Method name: "password" or "passkey" (case-insensitive).</param>
        /// <returns>True if the specified method is enabled; false otherwise.</returns>
        Task<bool> IsEnabledAsync(string methodName, CancellationToken ct);

        /// <summary>
        /// Updates auth method settings with partial updates (only provided fields are changed).
        /// Validates that at least one of password or passkey remains enabled.
        /// </summary>
        /// <param name="request">Settings update request with new values for auth methods.</param>
        /// <returns>True if settings were updated; false if validation failed (both methods disabled and no change persisted).</returns>
        Task<bool> UpdateAsync(UpdateAuthMethodSettingsRequest request, CancellationToken ct);
    }
}
