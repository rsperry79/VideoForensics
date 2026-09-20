using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingAuthService : IProviderAuthService
    {
        private const string ProviderName = "Ring";

        private readonly ILogger _logger;

        private static string MaskForLog(string value) =>
            string.IsNullOrEmpty(value) || value.Length <= 2 ? "***" : $"{value[0]}***{value[^1]}";
        private readonly ISessionProvider _sessionProvider;
        private readonly ICredentialStore _credentialStore;
        private readonly ICredentialRepository _credentialRepository;
        private readonly IRingAccountRepository? _ringAccountRepository;
        private readonly IProviderAccountRepository? _providerAccountRepository;
        private readonly IUserRepository? _userRepository;
        private readonly INotificationDispatcher? _notificationDispatcher;
        private readonly ApiResponseNormalizer? _normalizer;

        public RingAuthService(
            ILogger logger,
            ISessionProvider sessionProvider,
            ICredentialStore credentialStore,
            ICredentialRepository? credentialRepository = null,
            IRingAccountRepository? ringAccountRepository = null,
            IProviderAccountRepository? providerAccountRepository = null,
            IUserRepository? userRepository = null,
            INotificationDispatcher? notificationDispatcher = null,
            ApiResponseNormalizer? normalizer = null)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _credentialRepository = credentialRepository;
            _ringAccountRepository = ringAccountRepository;
            _providerAccountRepository = providerAccountRepository;
            _userRepository = userRepository;
            _notificationDispatcher = notificationDispatcher;
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
                _logger.LogInformation("Authenticating with Ring API for user: {Username}", MaskForLog(username));

                var credentials = new RingCredentials { UserName = username, Password = password };

                Session session = await Session.AuthenticateWithCredentials(
                    credentials,
                    twoFactorAuthCodeProvider: twoFactorAuthCodeProvider,
                    progress: null!
                );

                if (session?.OAuthToken != null)
                {
                    _sessionProvider.SetSession(session);
                    DateTime expiresAt = DateTime.UtcNow.AddHours(24);

                    // Persist credentials to secure store
                    if (credentials.RefreshToken != null)
                    {
                        // Credentials are persisted to database only - no filesystem storage
                    }

                    // Persist Ring account data to database and save refresh token
                    Guid? providerAccountId = null;
                    try
                    {
                        Guid resolvedAccountId = await GetOrCreateProviderAccountAsync(username, cancellationToken);
                        if (resolvedAccountId != Guid.Empty)
                        {
                            bool dbPersistenceSucceeded = true;

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
                                // Record successful auth to clear any previously-recorded error
                                try
                                {
                                    await _providerAccountRepository.RecordSuccessAsync(resolvedAccountId, cancellationToken);
                                }
                                catch (Exception recordEx)
                                {
                                    _logger.LogError(recordEx, "Failed to record authentication success for account {AccountId}", resolvedAccountId);
                                    // Non-fatal — auth itself succeeded
                                }
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
                                _logger.LogError("Database persistence failed for account {AccountId} — credentials must be in database", resolvedAccountId);
                                return new AuthResult(
                                    Success: false,
                                    ErrorMessage: "Failed to persist credentials to database"
                                );
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to resolve or create provider account");
                            return new AuthResult(
                                Success: false,
                                ErrorMessage: "Failed to create provider account record"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create provider account record");
                        return new AuthResult(
                            Success: false,
                            ErrorMessage: $"Failed to persist authentication: {ex.Message}"
                        );
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
        ///
        /// When no in-memory session exists yet (e.g. a fresh process start), falls back to
        /// attempting a restore from saved credentials (database refresh token) before
        /// reporting unauthenticated. Without this, every page that calls IsAuthenticatedAsync -
        /// Dashboard.razor and its ~13 siblings - reported "not signed in" on every app launch even
        /// for a previously-authenticated user, because restore was otherwise only wired into the
        /// /signin page's own OnInitializedAsync, which a normal cold start never visits.
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            Session? session = _sessionProvider.GetSession();
            if (session == null)
            {
                if (!await RestoreFromSavedCredentialsWithAccountAsync(providerAccountId: null, cancellationToken))
                {
                    return false;
                }

                session = _sessionProvider.GetSession();
                if (session == null)
                {
                    return false;
                }
            }

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

                Session? session = _sessionProvider.GetSession();
                if (session == null)
                {
                    return false;
                }

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
                Guid? resolvedAccountId = providerAccountId;

                // Try database first if providerAccountId provided
                if (providerAccountId.HasValue)
                {
                    try
                    {
                        (string CredentialType, string DecryptedValue)? credentialEntity = await _credentialRepository.GetAsync(
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
                        _logger.LogError(ex, "Failed to restore credentials from database for account {AccountId}", providerAccountId);
                        if (_providerAccountRepository != null && providerAccountId.HasValue)
                        {
                            try
                            {
                                await _providerAccountRepository.RecordErrorAsync(
                                    providerAccountId.Value,
                                    $"Failed to decrypt stored credential: {ex.Message}",
                                    cancellationToken);
                            }
                            catch (Exception recordEx)
                            {
                                _logger.LogError(recordEx, "Failed to record error for account {AccountId}", providerAccountId);
                            }
                        }

                        if (_notificationDispatcher != null)
                        {
                            try
                            {
                                await _notificationDispatcher.DispatchAsync(
                                    new NotificationEvent(
                                        EventType: "CredentialDecryptionFailed",
                                        TimestampUtc: DateTime.UtcNow,
                                        OperatorId: null,
                                        PairedDeviceId: null,
                                        SourceIp: null,
                                        Details: $"Failed to decrypt stored credential for account {providerAccountId.Value}: {ex.Message}",
                                        Audience: NotificationAudience.AdminsOnly,
                                        Severity: VideoForensics.Providers.Common.Contracts.NoticeSeverity.Critical),
                                    cancellationToken);
                            }
                            catch (Exception notifEx)
                            {
                                _logger.LogError(notifEx, "Failed to dispatch credential decryption failure notification for account {AccountId}", providerAccountId.Value);
                            }
                        }
                    }
                }
                else if (_providerAccountRepository != null)
                {
                    // If no specific account provided, try to find credentials from any Ring account
                    try
                    {
                        IReadOnlyList<ProviderAccount> ringAccounts = await _providerAccountRepository.ListActiveAsync(cancellationToken);
                        var ringAccountsForProvider = ringAccounts.Where(pa => pa.ProviderName == "Ring").ToList();

                        if (ringAccountsForProvider.Count > 0)
                        {
                            // Try each account until we find one with saved credentials
                            foreach (ProviderAccount? account in ringAccountsForProvider.OrderByDescending(a => a.LastSuccessfulAuthUtc))
                            {
                                try
                                {
                                    (string CredentialType, string DecryptedValue)? credentialEntity = await _credentialRepository.GetAsync(
                                        account.Id,
                                        "RefreshToken",
                                        cancellationToken);

                                    if (credentialEntity.HasValue && !string.IsNullOrWhiteSpace(credentialEntity.Value.DecryptedValue))
                                    {
                                        _logger.LogInformation("Restoring Ring session from database for account {AccountId}", account.Id);
                                        credentials = new RingCredentials { RefreshToken = credentialEntity.Value.DecryptedValue };
                                        resolvedAccountId = account.Id;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to restore credentials from database for account {AccountId}", account.Id);
                                    if (_providerAccountRepository != null)
                                    {
                                        try
                                        {
                                            await _providerAccountRepository.RecordErrorAsync(
                                                account.Id,
                                                $"Failed to decrypt stored credential: {ex.Message}",
                                                cancellationToken);
                                        }
                                        catch (Exception recordEx)
                                        {
                                            _logger.LogError(recordEx, "Failed to record error for account {AccountId}", account.Id);
                                        }
                                    }

                                    if (_notificationDispatcher != null)
                                    {
                                        try
                                        {
                                            await _notificationDispatcher.DispatchAsync(
                                                new NotificationEvent(
                                                    EventType: "CredentialDecryptionFailed",
                                                    TimestampUtc: DateTime.UtcNow,
                                                    OperatorId: null,
                                                    PairedDeviceId: null,
                                                    SourceIp: null,
                                                    Details: $"Failed to decrypt stored credential for account {account.Id}: {ex.Message}",
                                                    Audience: NotificationAudience.AdminsOnly,
                                                    Severity: VideoForensics.Providers.Common.Contracts.NoticeSeverity.Critical),
                                                cancellationToken);
                                        }
                                        catch (Exception notifEx)
                                        {
                                            _logger.LogError(notifEx, "Failed to dispatch credential decryption failure notification for account {AccountId}", account.Id);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to list Ring provider accounts");
                    }
                }

                // Credentials must be in database only - no filesystem fallback
                if (credentials?.RefreshToken == null)
                {
                    _logger.LogError("Ring credentials not found in database for account {AccountId}. Use --auth to save credentials to database.", providerAccountId);
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
                Session session = await Session.AuthenticateWithCredentials(credentials, twoFactorAuthCodeProvider: null, progress: null!);

                if (session?.OAuthToken == null)
                {
                    return false;
                }

                _sessionProvider.SetSession(resolvedAccountId ?? Guid.Empty, session);

                try
                {
                    // Update database if we have a provider account ID
                    if (resolvedAccountId == null || resolvedAccountId == Guid.Empty)
                    {
                        resolvedAccountId = string.IsNullOrWhiteSpace(credentials.UserName)
                            ? Guid.Empty
                            : await GetOrCreateProviderAccountAsync(credentials.UserName, cancellationToken);
                    }

                    if (resolvedAccountId != Guid.Empty)
                    {
                        await PersistRingAccountAsync(credentials.UserName ?? "unknown", session, resolvedAccountId.Value, cancellationToken);
                        // Record successful auth to clear any previously-recorded error
                        await _providerAccountRepository.RecordSuccessAsync(resolvedAccountId.Value, cancellationToken);
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
            {
                return Guid.Empty;
            }

            User? user = await _userRepository.GetByProviderKeyAsync(username, ct);
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

            ProviderAccount? account = await _providerAccountRepository.GetByUserAndProviderAsync(user.Id, ProviderName, ct);
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
            Session? session = _sessionProvider.GetSession();
            return session?.OAuthToken == null ? "Not authenticated" : "Authenticated";
        }

        private async Task PersistRingAccountAsync(string username, Session session, Guid providerAccountId, CancellationToken ct)
        {
            if (_ringAccountRepository == null)
            {
                return;
            }

            // Upsert by ProviderAccountId (unique) - re-authenticating an already-known account must
            // update the existing row, not insert a second one (RingAccounts.ProviderAccountId is
            // UNIQUE, so a blind insert on every auth fails with SQLite Error 19 the second time
            // around).
            RingAccount? existing = await _ringAccountRepository.GetByProviderAccountIdAsync(providerAccountId, ct);
            if (existing != null)
            {
                existing.AccountEmail = username;
                existing.AuthenticatedAtUtc = DateTime.UtcNow;
                await _ringAccountRepository.UpdateAsync(existing, ct);
            }
            else
            {
                var ringAccount = new RingAccount
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = providerAccountId,
                    SubscriptionLevel = "unknown",
                    AccountEmail = username,
                    AuthenticatedAtUtc = DateTime.UtcNow
                };
                await _ringAccountRepository.AddAsync(ringAccount, ct);
            }

            _logger.LogInformation("Persisted Ring authentication for {Username}", MaskForLog(username));
        }
    }
}
