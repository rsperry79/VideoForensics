using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Auth;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingAuthService : IProviderAuthService
    {
        private const string ProviderName = "Ring";

        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly ICredentialStore _credentialStore;
        private readonly IRingAccountRepository? _ringAccountRepository;
        private readonly IProviderAccountRepository? _providerAccountRepository;
        private readonly IUserRepository? _userRepository;
        private readonly ApiResponseNormalizer? _normalizer;
        private bool _isAuthenticated;

        public RingAuthService(
            ILogger logger,
            ISessionProvider sessionProvider,
            ICredentialStore credentialStore,
            IRingAccountRepository? ringAccountRepository = null,
            IProviderAccountRepository? providerAccountRepository = null,
            IUserRepository? userRepository = null,
            ApiResponseNormalizer? normalizer = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _ringAccountRepository = ringAccountRepository;
            _providerAccountRepository = providerAccountRepository;
            _userRepository = userRepository;
            _normalizer = normalizer;
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
                            _credentialStore.Save(CredentialResolver.AuthPath, credentials);
                            _logger.LogInformation("Credentials saved to secure store at {AuthPath}", CredentialResolver.AuthPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to persist credentials");
                        }
                    }

                    // Persist Ring account data to database
                    Guid? providerAccountId = null;
                    try
                    {
                        var resolvedAccountId = await GetOrCreateProviderAccountAsync(username, cancellationToken);
                        if (resolvedAccountId != Guid.Empty)
                        {
                            providerAccountId = resolvedAccountId;
                            await PersistRingAccountAsync(username, session, resolvedAccountId, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist Ring account data (non-fatal)");
                        // Continue - account data persistence is optional
                    }

                    return new AuthResult(
                        Success: true,
                        AuthToken: session.OAuthToken.AccessToken,
                        ExpiresAt: expiresAt,
                        ProviderAccountId: providerAccountId
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
            try
            {
                var saved = _credentialStore.Load(CredentialResolver.AuthPath);
                if (string.IsNullOrWhiteSpace(saved.RefreshToken))
                {
                    _logger.LogInformation("No saved refresh token found at {AuthPath}", CredentialResolver.AuthPath);
                    return false;
                }

                _logger.LogInformation("Restoring Ring session for {Username} from saved refresh token", saved.UserName);

                var session = await Session.AuthenticateWithCredentials(saved, twoFactorAuthCodeProvider: null, progress: null!);

                if (session?.OAuthToken == null)
                {
                    _isAuthenticated = false;
                    return false;
                }

                _sessionProvider.SetSession(session);
                _isAuthenticated = true;

                try
                {
                    _credentialStore.Save(CredentialResolver.AuthPath, saved);

                    var providerAccountId = await GetOrCreateProviderAccountAsync(saved.UserName ?? "unknown", cancellationToken);
                    await PersistRingAccountAsync(saved.UserName ?? "unknown", session, providerAccountId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist restored Ring account data (non-fatal)");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore session from saved credentials");
                _isAuthenticated = false;
                return false;
            }
        }

        private async Task<Guid> GetOrCreateProviderAccountAsync(string username, CancellationToken ct)
        {
            if (_userRepository == null || _providerAccountRepository == null)
                return Guid.Empty;

            var user = await _userRepository.GetByProviderKeyAsync(username, ct);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    ProviderUserKey = username,
                    DisplayName = username,
                    CreatedUtc = DateTime.UtcNow
                };
                await _userRepository.AddAsync(user, ct);
            }

            var account = await _providerAccountRepository.GetByUserAndProviderAsync(user.Id, ProviderName, ct);
            if (account == null)
            {
                account = new ProviderAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ProviderName = ProviderName,
                    LinkedUtc = DateTime.UtcNow,
                    LastSuccessfulAuthUtc = DateTime.UtcNow,
                    IsActive = true
                };
                await _providerAccountRepository.AddAsync(account, ct);
            }
            else
            {
                account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                account.IsActive = true;
                await _providerAccountRepository.UpdateAsync(account, ct);
            }

            return account.Id;
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

        private async Task PersistRingAccountAsync(string username, Session session, Guid providerAccountId, CancellationToken ct)
        {
            if (_ringAccountRepository == null)
                return;

            try
            {
                // Persist Ring account record for data governance
                var ringAccount = new RingAccount
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = providerAccountId,
                    SubscriptionLevel = "unknown",
                    AccountEmail = username,
                    AuthenticatedAtUtc = DateTime.UtcNow
                };

                await _ringAccountRepository.AddAsync(ringAccount, ct);
                _logger.LogInformation("Persisted Ring authentication for {Username}", username);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping account persistence (non-critical)");
            }
        }
    }
}
