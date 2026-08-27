using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingAuthService : IProviderAuthService
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly ICredentialStore _credentialStore;
        private bool _isAuthenticated;

        public RingAuthService(ILogger logger, ISessionProvider sessionProvider, ICredentialStore credentialStore)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
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

                    // Persist credentials to secure store
                    if (credentials.RefreshToken != null)
                    {
                        try
                        {
                            _credentialStore.Save(null!, credentials);
                            _logger.LogInformation("Credentials saved to secure store");
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
            // Credentials are now managed through the database via ProviderAccountRepository
            // This method is kept for compatibility but restoration happens at startup via account selection
            _logger.LogInformation("Credential restoration is managed through account selection");
            return false;
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
