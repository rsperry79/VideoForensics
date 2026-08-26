namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic authentication interface for video providers</summary>
    public interface IProviderAuthService
    {
        /// <summary>Authenticates with the provider using credentials</summary>
        Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

        /// <summary>Authenticates with the provider using credentials, with 2FA callback for providers that require it</summary>
        Task<AuthResult> AuthenticateWithTwoFactorAsync(string username, string password, Func<Task<string>> twoFactorAuthCodeProvider, CancellationToken cancellationToken = default);

        /// <summary>Checks if currently authenticated</summary>
        Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

        /// <summary>Refreshes authentication if needed</summary>
        Task<bool> RefreshAuthAsync(CancellationToken cancellationToken = default);

        /// <summary>Restores session from saved credentials (refresh token)</summary>
        Task<bool> RestoreFromSavedCredentialsAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets current authentication status</summary>
        string GetAuthStatus();
    }

    /// <summary>Result of authentication attempt</summary>
    public record AuthResult(
        bool Success,
        string? ErrorMessage = null,
        string? AuthToken = null,
        DateTime? ExpiresAt = null
    );
}
