using Microsoft.Extensions.Logging;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring;

namespace VideoForensics
{
    /// <summary>
    /// One-shot migrator that loads legacy credentials from the old JSON file and moves them into the encrypted credential store.
    /// This runs once at startup and is idempotent — if the old file doesn't exist or has already been migrated, it returns immediately.
    /// </summary>
    public class LegacyCredentialMigrator
    {
        private readonly ILogger<LegacyCredentialMigrator> _logger;
        private readonly IVideoForensicsDataClient _dataClient;
        private readonly string _credentialJsonPath;

        public LegacyCredentialMigrator(
            ILogger<LegacyCredentialMigrator> logger,
            IVideoForensicsDataClient dataClient,
            string credentialJsonPath)
        {
            _logger = logger;
            _dataClient = dataClient;
            _credentialJsonPath = credentialJsonPath;
        }

        /// <summary>
        /// Migrates credentials from the legacy JSON file to the encrypted credential store, if needed.
        /// Does not throw exceptions — logs and swallows errors to avoid blocking app startup.
        /// </summary>
        public async Task MigrateIfNeededAsync(CancellationToken ct)
        {
            try
            {
                // If the old file doesn't exist, nothing to migrate
                if (!File.Exists(_credentialJsonPath))
                {
                    _logger.LogInformation("Legacy credential file does not exist at {Path}, skipping migration", _credentialJsonPath);
                    return;
                }

                // Check if we've already migrated (idempotency guard)
                var migratedPath = _credentialJsonPath + ".migrated";
                if (File.Exists(migratedPath))
                {
                    _logger.LogInformation("Legacy credential file already migrated (found {Path}), skipping", migratedPath);
                    return;
                }

                _logger.LogInformation("Migrating legacy credentials from {Path} to encrypted credential store", _credentialJsonPath);

                // Load credentials from legacy file
                var credentialStore = new CredentialStore();
                var loadedCredentials = credentialStore.Load(_credentialJsonPath);

                // Ensure user and account exist with synthetic keys
                var (user, account) = await _dataClient.EnsureUserAndAccountAsync(
                    "Ring",
                    "default",
                    "default",
                    loadedCredentials.UserName,
                    ct);

                _logger.LogInformation("Ensured Ring account for user {UserName}", loadedCredentials.UserName ?? "unknown");

                // Migrate credentials to the new credential store
                if (!string.IsNullOrEmpty(loadedCredentials.Password))
                {
                    await _dataClient.Credentials.SetAsync(account.Id, "Password", loadedCredentials.Password, ct);
                    _logger.LogInformation("Migrated password credential to encrypted store");
                }

                if (!string.IsNullOrEmpty(loadedCredentials.RefreshToken))
                {
                    await _dataClient.Credentials.SetAsync(account.Id, "RefreshToken", loadedCredentials.RefreshToken, ct);
                    _logger.LogInformation("Migrated refresh token credential to encrypted store");
                }

                // Rename the old file to mark it as migrated (never delete — keep rollback path)
                if (!File.Exists(migratedPath))
                {
                    File.Move(_credentialJsonPath, migratedPath, overwrite: false);
                    _logger.LogInformation("Renamed legacy credential file to {Path} for archive", migratedPath);
                }

                _logger.LogInformation("Legacy credential migration completed successfully");
            }
            catch (Exception ex)
            {
                // Log the error but don't rethrow — migration failure shouldn't block the app.
                // The old credential file can still be used as a fallback by RestoreFromSavedCredentialsAsync.
                _logger.LogError(ex, "Legacy credential migration failed. App will continue, but old credential file will not be consumed. You may need to re-authenticate.");
            }
        }
    }
}
