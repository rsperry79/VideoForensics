namespace VideoForensics.Api.Contracts;

/// <summary>
/// Request DTO for the first step of authentication, accepting username and password.
/// </summary>
/// <param name="Username">The username/email credential for authentication with the provider.</param>
/// <param name="Password">The password credential for authentication with the provider.</param>
public record LoginRequestDto(
    string Username,
    string Password
);

/// <summary>
/// Request DTO for the second step of authentication when two-factor authentication is required.
/// Sent only when an initial <see cref="LoginRequestDto"/> is rejected with <see cref="AuthResultDto.RequiresTwoFactor"/> set to true.
/// </summary>
/// <param name="AuthAttemptId">The short-lived attempt identifier returned by the initial login call when two-factor authentication is required. This ties the second-step request back to the original authentication attempt.</param>
/// <param name="Code">The two-factor authentication code provided by the user (e.g., from an authenticator app or SMS).</param>
public record TwoFactorRequestDto(
    Guid AuthAttemptId,
    string Code
);

/// <summary>
/// Response DTO representing the result of an authentication attempt.
/// Mirrors the server-side <see cref="VideoForensics.Providers.Common.Contracts.AuthResult"/> record
/// and adds HTTP-serializable fields for handling two-factor authentication flows.
/// </summary>
/// <param name="Success">Indicates whether the authentication attempt succeeded. True if credentials were valid and no further action is required. False if credentials were rejected, or if an error occurred.</param>
/// <param name="ErrorMessage">Error message providing details if authentication failed. Null when <see cref="Success"/> is true or when the only issue is that two-factor authentication is required. Set to a provider-specific error message when credentials are invalid or an error condition occurs.</param>
/// <param name="AuthToken">Authentication token issued by the provider upon successful authentication. Null if <see cref="Success"/> is false or authentication is not yet complete (two-factor required). When present, this token can be used for subsequent API requests to the provider.</param>
/// <param name="ExpiresAt">Expiration time of the authentication token, in UTC. Null if <see cref="AuthToken"/> is null. When set, callers should refresh the authentication before this time if token refresh is supported.</param>
/// <param name="ProviderAccountId">The persisted provider account identifier that this login resolved to. Null if <see cref="Success"/> is false, if the provider implementation does not persist accounts, or if authentication is incomplete (two-factor required). Callers that track "the active account" (e.g., to attach downloaded devices to it) should initialize that state from this value after a successful login.</param>
/// <param name="RequiresTwoFactor">Indicates whether two-factor authentication is required to complete the login. True when the provider rejected the initial username/password and requires a second step with a time-sensitive code. False otherwise (including both success and one-step authentication failures). When true, the client should extract <see cref="AuthAttemptId"/>, collect the two-factor code from the user, and issue a second request with <see cref="TwoFactorRequestDto"/>.</param>
/// <param name="AuthAttemptId">Short-lived attempt identifier for two-factor authentication follow-up. Non-null only when <see cref="RequiresTwoFactor"/> is true; null otherwise. When set, this identifier uniquely ties the two-factor request back to this authentication attempt, allowing the server to validate that the 2FA code is being submitted for the correct login session.</param>
public record AuthResultDto(
    bool Success,
    string? ErrorMessage = null,
    string? AuthToken = null,
    DateTime? ExpiresAt = null,
    Guid? ProviderAccountId = null,
    bool RequiresTwoFactor = false,
    Guid? AuthAttemptId = null
);
