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
        private readonly ICredentialRepository _credentialRepository;
        private readonly IRingAccountRepository? _ringAccountRepository;
        private readonly IProviderAccountRepository? _providerAccountRepository;
        private readonly IUserRepository? _userRepository;
        private readonly ApiResponseNormalizer? _normalizer;

        public RingAuthService(
            ILogger logger,
            ISessionProvider sessionProvider,
            ICredentialStore credentialStore,
            ICredentialRepository? credentialRepository = null,
            IRingAccountRepository? ringAccountRepository = null,
            IProviderAccountRepository? providerAccountRepository = null,
            IUserRepository? userRepository = null,
            ApiResponseNormalizer? normalizer = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _credentialRepository = credentialRepository;
            _ringAccountRepository = ringAccountRepository;
            _providerAccountRepository = providerAccountRepository;
            _userRepository = userRepository;
            _normalizer = normalizer;
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
                            _logger.LogError(ex, "Failed to persist credentials to filesystem");
                        }
                    }

                    // Persist Ring account data to database and save refresh token
                    Guid? providerAccountId = null;
                    try
                    {
                        var resolvedAccountId = await GetOrCreateProviderAccountAsync(username, cancellationToken);
                        if (resolvedAccountId != Guid.Empty)
                        {
                            var dbPersistenceSucceeded = true;

                            // Dual-write: save refresh token to database
                            if (session.OAuthToken.RefreshToken != null)
                            {
                                try
                                {
                                    await _credentialRepository.SetAsync(
                                        resolvedAccountId,
                                        "RefreshToken",
                                        session.OAuthToken.RefreshToken,
                                        cancellationToken);
                                    _logger.LogInformation("Refresh token saved to database for account {AccountId}", resolvedAccountId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to save refresh token to database for account {AccountId}", resolvedAccountId);
                                    dbPersistenceSucceeded = false;
                                }
                            }

                            try
                            {
                                await PersistRingAccountAsync(username, session, resolvedAccountId, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to persist Ring account metadata for account {AccountId}", resolvedAccountId);
                                dbPersistenceSucceeded = false;
                            }

                            // Only report ProviderAccountId if database persistence succeeded
                            if (dbPersistenceSucceeded)
                            {
                                providerAccountId = resolvedAccountId;
                            }
                            else
                            {
                                _logger.LogWarning("Database persistence failed for account {AccountId} — using filesystem-only fallback", resolvedAccountId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create provider account record");
                        // Continue - account creation is optional, refresh token is persisted to filesystem
                    }

                    return new AuthResult(
                        Success: true,
                        AuthToken: session.OAuthToken.AccessToken,
                        ExpiresAt: expiresAt,
                        ProviderAccountId: providerAccountId
                    );
                }

                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Authentication failed - no token returned"
                );
            }
            catch (Exceptions.TwoFactorAuthenticationIncorrectException)
            {
                _logger.LogError("Two-factor authentication code was incorrect");
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Two-factor authentication code was incorrect. Please try again."
                );
            }
            catch (Exceptions.TwoFactorAuthenticationRequiredException)
            {
                _logger.LogError("Two-factor authentication is required but no 2FA callback was provided");
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Two-factor authentication is required. Please provide your 2FA code."
                );
            }
            catch (Exceptions.AuthenticationFailedException ex)
            {
                _logger.LogError(ex, "Authentication failed");
                return new AuthResult(
                    Success: false,
                    ErrorMessage: "Invalid email or password"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error");
                return new AuthResult(
                    Success: false,
                    ErrorMessage: ex.Message
                );
            }
        }

        /// <summary>
        /// Checks whether there's a currently-valid Ring session. Deliberately derives this purely
        /// from ISessionProvider (the shared, process-wide source of truth for session state) rather
        /// than any per-instance flag - this class is registered Scoped (one instance per DI scope/
        /// circuit), so a per-instance "am I authenticated" flag would incorrectly read false on a
        /// fresh scope even when a valid session already exists, set by a different scope's instance.
        /// Caught by actually running the Web app: a fresh page load (new circuit) reported "not
        /// signed in" immediately after a successful sign-in in a different circuit, even though the
        /// shared session was still valid.
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            var session = _sessionProvider.GetSession();
            if (session == null)
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
            return await RestoreFromSavedCredentialsWithAccountAsync(providerAccountId: null, cancellationToken);
        }

        public async Task<bool> RestoreFromSavedCredentialsAsync(
            Guid? providerAccountId,
            CancellationToken cancellationToken = default)
        {
            return await RestoreFromSavedCredentialsWithAccountAsync(providerAccountId, cancellationToken);
        }

        public async Task<bool> RestoreFromSavedCredentialsWithAccountAsync(
            Guid? providerAccountId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                RingCredentials? credentials = null;

                // Try database first if providerAccountId provided
                if (providerAccountId.HasValue)
                {
                    try
                    {
                        var credentialEntity = await _credentialRepository.GetAsync(
                            providerAccountId.Value,
                            "RefreshToken",
                            cancellationToken);

                        if (credentialEntity.HasValue && !string.IsNullOrWhiteSpace(credentialEntity.Value.DecryptedValue))
                        {
                            _logger.LogInformation("Restoring Ring session from database for account {AccountId}", providerAccountId);
                            credentials = new RingCredentials { RefreshToken = credentialEntity.Value.DecryptedValue };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore credentials from database for account {AccountId}, falling back to filesystem", providerAccountId);
                    }
                }

                // Fall back to filesystem (backward compatibility)
                if (credentials?.RefreshToken == null)
                {
                    try
                    {
                        var saved = _credentialStore.Load(CredentialResolver.AuthPath);
                        if (!string.IsNullOrWhiteSpace(saved.RefreshToken))
                        {
                            credentials = saved;
                            _logger.LogWarning("Restoring Ring credentials from filesystem - consider migrating to database storage");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore credentials from filesystem");
                    }
                }

                if (credentials?.RefreshToken == null)
                {
                    _logger.LogInformation("No saved refresh token found");
                    return false;
                }

                _logger.LogInformation("Restoring Ring session from refresh token");

                if (credentials.RefreshToken == null)
                {
                    _logger.LogError("Cannot restore session: refresh token is null");
                    return false;
                }

                // For refresh token flow, we don't need 2FA — Ring API handles it server-side
                var session = await Session.AuthenticateWithCredentials(credentials, twoFactorAuthCodeProvider: null, progress: null!);

                if (session?.OAuthToken == null)
                {
                    return false;
                }

                _sessionProvider.SetSession(session);

                try
                {
                    // Update filesystem for backward compatibility
                    _credentialStore.Save(CredentialResolver.AuthPath, credentials);

                    // Update database if we have a provider account ID
                    var resolvedAccountId = providerAccountId ?? (
                        string.IsNullOrWhiteSpace(credentials.UserName)
                            ? Guid.Empty
                            : await GetOrCreateProviderAccountAsync(credentials.UserName, cancellationToken)
                    );

                    if (resolvedAccountId != Guid.Empty)
                    {
                        await PersistRingAccountAsync(credentials.UserName ?? "unknown", session, resolvedAccountId, cancellationToken);
                    }
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
            var session = _sessionProvider.GetSession();
            if (session?.OAuthToken == null)
                return "Not authenticated";

            return "Authenticated";
        }

        private async Task PersistRingAccountAsync(string username, Session session, Guid providerAccountId, CancellationToken ct)
        {
            if (_ringAccountRepository == null)
                return;

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
    }
}
