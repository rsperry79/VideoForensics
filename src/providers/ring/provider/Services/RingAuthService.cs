using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingAuthService : IProviderAuthService
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly ICredentialStore? _credentialStore;
        private readonly string? _credentialPath;
        private bool _isAuthenticated;

        public RingAuthService(ILogger logger, ISessionProvider sessionProvider, ICredentialStore? credentialStore = null, string? credentialPath = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _credentialPath = credentialPath;
            _credentialStore = credentialStore;
            _isAuthenticated = false;
        }

        public async Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            return await AuthenticateWithTwoFactorAsync(username, password, twoFactorAuthCodeProvider: null!, cancellationToken);
        }

        public async Task<AuthResult> AuthenticateWithTwoFactorAsync(string username, string password, Func<Task<string>> twoFactorAuthCodeProvider, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Authenticating with Ring API for user: {Username}", username);

                var credentials = new RingCredentials { UserName = username, Password = password };

                var session = await Session.AuthenticateWithCredentials(
                    credentials,
                    twoFactorAuthCodeProvider: twoFactorAuthCodeProvider,
                    progress: null!
                );

                if (session?.OAuthToken != null)
                {
                    _sessionProvider.SetSession(session);
                    _isAuthenticated = true;
                    var expiresAt = DateTime.UtcNow.AddHours(24);

                    // Persist credentials if credential store is configured
                    if (_credentialStore != null && credentials.RefreshToken != null)
                    {
                        try
                        {
                            _credentialStore.Save(_credentialPath, credentials);
                            _logger.LogInformation("Credentials saved to {CredentialPath}", _credentialPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to persist credentials");
                        }
                    }

                    return new AuthResult(
                        Success: true,
                        AuthToken: session.OAuthToken.AccessToken,
                        ExpiresAt: expiresAt
                    );
                }

                _isAuthenticated = false;
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Authentication failed - no token returned"
                );
            }
            catch (Exceptions.TwoFactorAuthenticationIncorrectException)
            {
                _logger.LogError("Two-factor authentication code was incorrect");
                _isAuthenticated = false;
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Two-factor authentication code was incorrect. Please try again."
                );
            }
            catch (Exceptions.TwoFactorAuthenticationRequiredException)
            {
                _logger.LogError("Two-factor authentication is required but no 2FA callback was provided");
                _isAuthenticated = false;
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Two-factor authentication is required. Please provide your 2FA code."
                );
            }
            catch (Exceptions.AuthenticationFailedException ex)
            {
                _logger.LogError(ex, "Authentication failed");
                _isAuthenticated = false;
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Invalid email or password"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error");
                _isAuthenticated = false;
                return new AuthResult(
                    Success: false,
                    ErrorMessage: ex.Message
                );
            }
        }

        public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            var session = _sessionProvider.GetSession();
            if (session == null || !_isAuthenticated)
                return false;

            try
            {
                await session.EnsureSessionValid();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RefreshAuthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Refreshing Ring API token");

                var session = _sessionProvider.GetSession();
                if (session == null)
                    return false;

                await session.RefreshSession();
                _isAuthenticated = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return false;
            }
        }

        public async Task<bool> RestoreFromSavedCredentialsAsync(CancellationToken cancellationToken = default)
        {
            if (_credentialStore == null)
            {
                _logger.LogInformation("No credential store configured for restoration");
                return false;
            }

            try
            {
                var saved = _credentialStore.Load(_credentialPath);
                if (string.IsNullOrEmpty(saved.RefreshToken))
                {
                    _logger.LogInformation("No saved refresh token found");
                    return false;
                }

                _logger.LogInformation("Attempting to restore session from saved refresh token for user {Username}", saved.UserName);

                var session = await Session.AuthenticateWithCredentials(
                    saved,
                    twoFactorAuthCodeProvider: null!,
                    progress: null!
                );

                if (session?.OAuthToken != null)
                {
                    _sessionProvider.SetSession(session);
                    _isAuthenticated = true;
                    _logger.LogInformation("Session restored successfully for user {Username}", saved.UserName);
                    return true;
                }

                _logger.LogWarning("Refresh token validation failed, credentials may be stale");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore session from saved credentials");
                return false;
            }
        }

        public string GetAuthStatus()
        {
            if (!_isAuthenticated)
                return "Not authenticated";

            var session = _sessionProvider.GetSession();
            if (session?.OAuthToken == null)
                return "No session";

            return "Authenticated";
        }
    }
}
