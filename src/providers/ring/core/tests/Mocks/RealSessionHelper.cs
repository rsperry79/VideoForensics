using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
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
            services.AddLogging();

            // Same fixed key-ring location and DAPI protection (Windows-only) as every other host -
            // must match, or a refresh token saved by one host can't be decrypted by another.
            string dataProtectionKeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
            _ = Directory.CreateDirectory(dataProtectionKeyPath);
            var dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            if (OperatingSystem.IsWindows())
            {
                dataProtectionBuilder.ProtectKeysWithDpapi();
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
            var authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
            var sessionProvider = sp.GetRequiredService<ISessionProvider>();

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

            if (!restored)
            {
                throw new InvalidOperationException(
                    $"Ring API credentials not found in database.\n{SetupPointer}");
            }

            return sessionProvider.GetSession()
                ?? throw new InvalidOperationException(
                    $"Credentials restored but no session was established.\n{SetupPointer}");
        }

        /// <summary>
        /// Creates a session without authenticating (for testing session creation only). Requires
        /// username/password, which the database never stores (only an encrypted refresh token,
        /// per this app's no-plain-text-passwords policy) - so this always throws. Kept for callers
        /// that already guard on <see cref="UsernamePasswordAvailable"/> first.
        /// </summary>
        public static Session CreateSessionWithoutAuth()
        {
            throw new InvalidOperationException(
                $"Ring API username/password is never stored (only an encrypted refresh token).\n{SetupPointer}");
        }

        /// <summary>
        /// Checks if a saved refresh token is available for real integration testing.
        /// </summary>
        public static bool CredentialsAvailable()
        {
            try
            {
                IServiceProvider sp = ServiceProviderLazy.Value;
                using IServiceScope scope = sp.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
                return authService.RestoreFromSavedCredentialsAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Always false - the database never stores a raw username/password, only an encrypted
        /// refresh token. Kept so existing tests that guard on this before calling
        /// <see cref="CreateSessionWithoutAuth"/> continue to skip cleanly instead of failing.
        /// </summary>
        public static bool UsernamePasswordAvailable()
        {
            return false;
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
                var authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
                var sessionProvider = sp.GetRequiredService<ISessionProvider>();
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
