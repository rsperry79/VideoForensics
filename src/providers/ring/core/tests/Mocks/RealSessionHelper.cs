using System.Threading;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Providers.Ring.Core.Tests.Mocks
{
    /// <summary>
    /// Helper class for creating real Ring API sessions for integration testing. Restores
    /// credentials from the database via the same DI composition root (AddVideoForensicsDataLayer +
    /// AddVideoForensicsServerCore("Ring")) that WebApp, the legacy console app, and SelfTester use -
    /// so these tests exercise the actual database-backed RingAuthService, not a separate credential
    /// path of their own.
    /// Never performs its own interactive/2FA authentication (tests aren't interactive) - if
    /// credentials aren't available or don't work, callers get a clear error pointing at how to fix
    /// it via the SelfTester authentication flow.
    ///
    /// To populate the database with credentials:
    ///   cd src/selftest
    ///   dotnet run -- --auth
    /// </summary>
    public static class RealSessionHelper
    {
        private const string SetupPointer =
            "Run 'dotnet run -- --auth' from src/selftest to authenticate (handles two-factor " +
            "accounts too) and save credentials to the database.";

        private static readonly Lazy<IServiceProvider> ServiceProviderLazy = new(BuildServiceProvider);

        private static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();

            // Same fixed key-ring location and DAPI protection (Windows-only) as every other host -
            // must match, or a refresh token saved by one host can't be decrypted by another.
            string dataProtectionKeyPath = new StorageLocationProvider().GetDefaultRoot(StorageCategory.Keys);
            _ = Directory.CreateDirectory(dataProtectionKeyPath);
            IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            if (OperatingSystem.IsWindows())
            {
                _ = dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);
            }

            _ = services.AddVideoForensicsDataLayer();
            _ = services.AddVideoForensicsServerCore("Ring");

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Creates and authenticates a session by restoring the active (or most-recently-authenticated)
        /// Ring account's saved refresh token from the database.
        /// Throws InvalidOperationException, with setup instructions, if no credentials are saved or
        /// they no longer work.
        /// </summary>
        public static async Task<Session> CreateAuthenticatedSessionAsync()
        {
            IServiceProvider sp = ServiceProviderLazy.Value;
            using IServiceScope scope = sp.CreateScope();
            IProviderAuthService authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
            ISessionProvider sessionProvider = sp.GetRequiredService<ISessionProvider>();

            bool restored;
            try
            {
                restored = await authService.RestoreFromSavedCredentialsAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to authenticate with the Ring API using the saved credentials in the database. " +
                    $"They may be stale or invalid.\n{SetupPointer}\nUnderlying error: {ex.Message}", ex);
            }

            return !restored
                ? throw new InvalidOperationException(
                    $"Ring API credentials not found in database.\n{SetupPointer}")
                : sessionProvider.GetSession()
                ?? throw new InvalidOperationException(
                    $"Credentials restored but no session was established.\n{SetupPointer}");
        }

        /// <summary>
        /// Restores the saved credentials through the same auth service the Query page uses, then returns the
        /// non-secret status payload that page shows. Throws, with setup instructions, if nothing restores.
        /// </summary>
        public static async Task<ProviderAuthStatus?> RestoreAndGetAuthStatusAsync()
        {
            IServiceProvider sp = ServiceProviderLazy.Value;
            using IServiceScope scope = sp.CreateScope();
            IProviderAuthService authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();

            bool restored = await authService.RestoreFromSavedCredentialsAsync();
            return !restored
                ? throw new InvalidOperationException($"Ring API credentials not found in database.\n{SetupPointer}")
                : authService.GetAuthStatusDetails();
        }

        /// <summary>The same composition root the helper builds. Live tests that need the saved database resolve repositories from it.</summary>
        public static IServiceProvider Services => ServiceProviderLazy.Value;

        private enum CredentialState
        {
            /// <summary>No Ring account in the database holds a refresh token. Live tests skip.</summary>
            None,

            /// <summary>A refresh token is saved and restores a session. Live tests run.</summary>
            Usable,

            /// <summary>A refresh token is saved but does not restore. Live tests fail, so a broken login is not hidden.</summary>
            Broken,
        }

        // Probed once per test process: restoring a session calls the Ring API, so every live test must not repeat it.
        private static readonly Lazy<CredentialState> CredentialStateLazy = new(ProbeCredentialState);

        /// <summary>
        /// True when the database holds usable Ring credentials. Live tests run whenever this is true, so no environment
        /// variable is needed to enable them.
        /// </summary>
        public static bool CredentialsAvailable()
        {
            return CredentialStateLazy.Value == CredentialState.Usable;
        }

        /// <summary>
        /// Skips the calling test when no Ring credentials are saved, and fails it when they are saved but do not restore.
        /// A skip is only for "nothing to test"; saved credentials that no longer work are a failure.
        /// </summary>
        public static void RequireLive()
        {
            switch (CredentialStateLazy.Value)
            {
                case CredentialState.None:
                    Xunit.Assert.Skip("No Ring credentials in the database. " + SetupPointer);
                    break;
                case CredentialState.Broken:
                    Xunit.Assert.Fail("Ring credentials are saved in the database but did not restore a session. " + SetupPointer);
                    break;
            }
        }

        private static CredentialState ProbeCredentialState()
        {
            IServiceProvider sp = ServiceProviderLazy.Value;
            using IServiceScope scope = sp.CreateScope();
            IProviderAccountRepository accounts = scope.ServiceProvider.GetRequiredService<IProviderAccountRepository>();
            ICredentialRepository credentials = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();

            IReadOnlyList<ProviderAccount> ringAccounts = accounts.ListAsync(CancellationToken.None).GetAwaiter().GetResult();
            bool hasRefreshToken = false;
            foreach (ProviderAccount account in ringAccounts.Where(a => a.ProviderName == "Ring"))
            {
                IReadOnlyList<Credential> saved = credentials.GetByProviderAccountIdAsync(account.Id, CancellationToken.None).GetAwaiter().GetResult();
                hasRefreshToken |= saved.Any(c => c.CredentialType == "RefreshToken");
            }

            if (!hasRefreshToken)
            {
                return CredentialState.None;
            }

            try
            {
                IProviderAuthService authService = sp.CreateScope().ServiceProvider.GetRequiredService<IProviderAuthService>();
                return authService.RestoreFromSavedCredentialsAsync().GetAwaiter().GetResult() ? CredentialState.Usable : CredentialState.Broken;
            }
            catch
            {
                return CredentialState.Broken;
            }
        }

        /// <summary>
        /// Gets a message explaining how to setup credentials, for tests that want to surface it directly.
        /// </summary>
        public static string GetSetupInstructions()
        {
            return $"Ring API credentials not found or not usable in database.\n{SetupPointer}";
        }

        /// <summary>
        /// Returns the saved refresh token from the database (restoring a session in the process),
        /// or null if none is available/valid. For legacy test fixtures (UnitTest.cs) that want just
        /// the raw token string rather than a live Session.
        /// </summary>
        public static string? TryGetSavedRefreshToken()
        {
            try
            {
                IServiceProvider sp = ServiceProviderLazy.Value;
                using IServiceScope scope = sp.CreateScope();
                IProviderAuthService authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
                ISessionProvider sessionProvider = sp.GetRequiredService<ISessionProvider>();
                bool restored = authService.RestoreFromSavedCredentialsAsync().GetAwaiter().GetResult();
                return restored ? sessionProvider.GetSession()?.OAuthToken?.RefreshToken : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
