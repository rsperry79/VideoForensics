using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Hosting;
using VideoForensics.Core.Providers.Common.Contracts;
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
            string dataProtectionKeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
            _ = Directory.CreateDirectory(dataProtectionKeyPath);
            IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            if (OperatingSystem.IsWindows())
            {
                _ = dataProtectionBuilder.ProtectKeysWithDpapi();
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

        // Probed once per test process: restoring a session calls the Ring API, so every live test must not repeat it.
        private static readonly Lazy<bool> CredentialsPresent = new(ProbeSavedCredentials);

        /// <summary>
        /// True when the database holds usable Ring credentials. Live tests run whenever this is true and skip
        /// otherwise, so no environment variable is needed to enable them.
        /// </summary>
        public static bool CredentialsAvailable() => CredentialsPresent.Value;

        /// <summary>Skips the calling test when no Ring credentials are saved in the database.</summary>
        public static void RequireLive()
        {
            if (!CredentialsAvailable())
            {
                Xunit.Assert.Skip("No Ring credentials in the database. " + SetupPointer);
            }
        }

        private static bool ProbeSavedCredentials()
        {
            try
            {
                IServiceProvider sp = ServiceProviderLazy.Value;
                using IServiceScope scope = sp.CreateScope();
                IProviderAuthService authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
                return authService.RestoreFromSavedCredentialsAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return false;
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
