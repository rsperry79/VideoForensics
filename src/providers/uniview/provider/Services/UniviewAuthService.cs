using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Uniview;

namespace VideoForensics.Providers.Uniview.Services
{
    /// <summary>
    /// Authentication service for Uniview NVR devices.
    /// Uniview uses HTTP Digest authentication against a local NVR device with no cloud account or token concept.
    /// </summary>
    public class UniviewAuthService : IProviderAuthService
    {
        private const string ProviderName = "Uniview";

        private readonly ILogger<UniviewAuthService> _logger;
        private readonly IUniviewSessionProvider _sessionProvider;
        private readonly IForensicsConfiguration _configuration;
        private readonly ICredentialRepository _credentialRepository;

        public UniviewAuthService(
            ILogger<UniviewAuthService> logger,
            IUniviewSessionProvider sessionProvider,
            IForensicsConfiguration configuration,
            ICredentialRepository credentialRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        }

        /// <summary>
        /// Authenticates with a Uniview NVR using username and password via HTTP Digest auth.
        /// </summary>
        public async Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Authenticating with Uniview NVR for user: {Username}", username);

                var host = _configuration.UniviewNvrHost ?? throw new InvalidOperationException("Uniview NVR host is not configured");
                var ffmpegPath = _configuration.UniviewFfmpegPath ?? "ffmpeg";

                var client = new UniviewClient(host, username, password, ffmpegPath);

                await client.LoginAsync(cancellationToken);
                _logger.LogInformation("Successfully authenticated with Uniview NVR for user: {Username}", username);

                // Store the authenticated client in the session provider
                _sessionProvider.SetClient(client);

                // Persist credentials to the database for later restoration
                Guid? providerAccountId = null;
                try
                {
                    providerAccountId = await PersistCredentialsAsync(username, password, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Uniview credentials (non-fatal)");
                    // Continue - the session is valid even if database persistence fails
                }

                // Uniview uses HTTP Digest auth with no bearer token or cloud account model
                // AuthToken is null (no token concept), ExpiresAt is null (sessions kept alive via KeepAliveAsync)
                return new AuthResult(
                    Success: true,
                    AuthToken: null,
                    ExpiresAt: null,
                    ProviderAccountId: providerAccountId
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Uniview authentication failed: {Message}", ex.Message);
                return new AuthResult(
                    Success: false,
                    ErrorMessage: ex.Message
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uniview authentication error");
                return new AuthResult(
                    Success: false,
                    ErrorMessage: ex.Message
                );
            }
        }

        /// <summary>
        /// Uniview (local NVR, HTTP Digest auth) has no two-factor authentication concept.
        /// This method ignores the 2FA callback and simply delegates to <see cref="AuthenticateAsync"/>.
        /// </summary>
        public async Task<AuthResult> AuthenticateWithTwoFactorAsync(
            string username,
            string password,
            Func<Task<string>> twoFactorAuthCodeProvider,
            CancellationToken cancellationToken = default)
        {
            // Uniview has no 2FA - ignore the callback and authenticate normally
            return await AuthenticateAsync(username, password, cancellationToken);
        }

        /// <summary>
        /// Checks if currently authenticated with a Uniview NVR.
        /// Returns true if a session exists with an authenticated client.
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _sessionProvider.GetClient();
                if (client == null)
                {
                    return false;
                }

                // Verify the session is still alive by calling KeepAliveAsync
                // If it throws, the session is no longer valid
                await client.KeepAliveAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Refreshes the authentication by sending a KeepAlive signal to the NVR.
        /// Returns true if successful, false otherwise.
        /// </summary>
        public async Task<bool> RefreshAuthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _sessionProvider.GetClient();
                if (client == null)
                {
                    return false;
                }

                await client.KeepAliveAsync(cancellationToken);
                _logger.LogInformation("Uniview session refreshed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh Uniview session");
                return false;
            }
        }

        /// <summary>
        /// Restores authentication from saved credentials stored in the database.
        /// </summary>
        public async Task<bool> RestoreFromSavedCredentialsAsync(CancellationToken cancellationToken = default)
        {
            // For the parameterless overload, we don't have a specific provider account ID
            // Try to restore from any available saved credentials
            return await RestoreFromSavedCredentialsWithAccountAsync(providerAccountId: null, cancellationToken);
        }

        /// <summary>
        /// Restores authentication from saved credentials for a specific Uniview account.
        /// </summary>
        public async Task<bool> RestoreFromSavedCredentialsAsync(Guid? providerAccountId, CancellationToken cancellationToken = default)
        {
            return await RestoreFromSavedCredentialsWithAccountAsync(providerAccountId, cancellationToken);
        }

        private async Task<bool> RestoreFromSavedCredentialsWithAccountAsync(Guid? providerAccountId, CancellationToken cancellationToken)
        {
            try
            {
                string? username = null;
                string? password = null;

                // If a specific account ID is provided, try to restore from database
                if (providerAccountId.HasValue)
                {
                    try
                    {
                        var usernameCred = await _credentialRepository.GetAsync(providerAccountId.Value, "Username", cancellationToken);
                        var passwordCred = await _credentialRepository.GetAsync(providerAccountId.Value, "Password", cancellationToken);

                        if (usernameCred.HasValue && !string.IsNullOrWhiteSpace(usernameCred.Value.DecryptedValue))
                        {
                            username = usernameCred.Value.DecryptedValue;
                        }

                        if (passwordCred.HasValue && !string.IsNullOrWhiteSpace(passwordCred.Value.DecryptedValue))
                        {
                            password = passwordCred.Value.DecryptedValue;
                        }

                        if (username != null && password != null)
                        {
                            _logger.LogInformation("Restoring Uniview session from database for account {AccountId}", providerAccountId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore credentials from database for account {AccountId}", providerAccountId);
                    }
                }

                // If we have credentials, try to authenticate
                if (username != null && password != null)
                {
                    var result = await AuthenticateAsync(username, password, cancellationToken);
                    if (result.Success)
                    {
                        _logger.LogInformation("Successfully restored Uniview session from saved credentials");
                        return true;
                    }
                }

                _logger.LogInformation("No saved Uniview credentials found or restore failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore Uniview session from saved credentials");
                return false;
            }
        }

        /// <summary>
        /// Gets the current authentication status.
        /// </summary>
        public string GetAuthStatus()
        {
            var client = _sessionProvider.GetClient();
            return client != null ? "Authenticated" : "Not authenticated";
        }

        /// <summary>
        /// Persists Uniview credentials to the database for later restoration.
        /// Returns the provider account ID if successful, null otherwise.
        /// </summary>
        private async Task<Guid?> PersistCredentialsAsync(string username, string password, CancellationToken cancellationToken)
        {
            try
            {
                // Use a fixed well-known ID for the Uniview provider account, since Uniview is a local device
                // with no cloud account model. This allows all Uniview sessions to be stored under a single account.
                var providerAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

                // Save username and password to the database (encrypted automatically by ICredentialRepository)
                await _credentialRepository.SetAsync(providerAccountId, "Username", username, cancellationToken);
                await _credentialRepository.SetAsync(providerAccountId, "Password", password, cancellationToken);

                _logger.LogInformation("Uniview credentials persisted to database");
                return providerAccountId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist Uniview credentials");
                return null;
            }
        }
    }
}
