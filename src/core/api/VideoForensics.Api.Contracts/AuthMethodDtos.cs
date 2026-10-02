namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Response DTO for public authentication methods query (GET /api/v1/auth/methods).
    /// Returns enabled flags for password and passkey authentication only.
    /// </summary>
    /// <param name="Password">Whether password-based authentication is enabled.</param>
    /// <param name="Passkey">Whether passkey/WebAuthn authentication is enabled.</param>
    public record AuthMethodsDto(
        bool Password,
        bool Passkey
    );

    /// <summary>
    /// Response DTO for the auth method settings query (GET /api/v1/auth-methods/settings).
    /// Includes toggles for password and passkey authentication only.
    /// </summary>
    /// <param name="PasswordEnabled">Whether password-based authentication is enabled.</param>
    /// <param name="PasskeyEnabled">Whether passkey/WebAuthn authentication is enabled.</param>
    public record AuthMethodSettingsDto(
        bool PasswordEnabled,
        bool PasskeyEnabled
    );

    /// <summary>
    /// Request DTO for updating auth method settings (PUT /api/v1/auth-methods/settings).
    /// Partial update - only fields provided are modified.
    /// </summary>
    /// <param name="PasswordEnabled">Enable or disable password-based authentication.</param>
    /// <param name="PasskeyEnabled">Enable or disable passkey/WebAuthn authentication.</param>
    public record UpdateAuthMethodSettingsRequest(
        bool? PasswordEnabled = null,
        bool? PasskeyEnabled = null
    );
}
