using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Auth;

using System;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring.Tests.Mocks
{
    /// <summary>
    /// Helper class for creating real Ring API sessions for integration testing. Reads the shared
    /// credentials file (refresh token, or username/password) via <see cref="CredentialResolver"/> -
    /// the same file ApiTester's `--auth` flow writes to, and that RingVideos may also write to.
    /// Never performs its own interactive/2FA authentication (tests aren't interactive) - if
    /// credentials aren't available or don't work, callers get a clear error pointing at how to fix
    /// it via ApiTester rather than this project's own auth code.
    ///
    /// To populate the credentials file:
    ///   cd external/Ring.Api/selftest
    ///   dotnet run -- --auth
    /// See external/Ring.Api/README.md ("Authenticating for local tooling") for details.
    /// </summary>
    public class RealSessionHelper
    {
        private const string SetupPointer =
            "Run 'dotnet run -- --auth' from external/Ring.Api/selftest to authenticate (handles two-factor " +
            "accounts too) and save a reusable refresh token. See external/Ring.Api/README.md " +
            "(\"Authenticating for local tooling\") for details.";

        /// <summary>
        /// Creates and authenticates a session using the shared credentials file, preferring a
        /// saved refresh token over username/password (matches ApiTester's own priority).
        /// Throws InvalidOperationException, with setup instructions, if credentials are missing,
        /// invalid, or the account requires an interactive two-factor challenge this helper cannot
        /// complete.
        /// </summary>
        public static async Task<Session> CreateAuthenticatedSessionAsync()
        {
            var auth = CredentialResolver.Resolve(null, null, null);
            if (auth == null)
            {
                throw new InvalidOperationException(
                    $"Ring API credentials not found at {CredentialResolver.AuthPath}.\n{SetupPointer}");
            }

            try
            {
                if (!string.IsNullOrEmpty(auth.RefreshToken))
                {
                    return await Session.GetSessionByRefreshToken(auth.RefreshToken);
                }

                var session = new Session(auth.UserName, auth.Password);
                await session.Authenticate();
                return session;
            }
            catch (Ring.Api.Exceptions.TwoFactorAuthenticationRequiredException)
            {
                throw new InvalidOperationException(
                    $"The saved credentials require two-factor authentication, which this test helper cannot complete " +
                    $"interactively.\n{SetupPointer}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to authenticate with the Ring API using the saved credentials at {CredentialResolver.AuthPath}. " +
                    $"They may be stale or invalid.\n{SetupPointer}\nUnderlying error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a session without authenticating (for testing session creation only). Requires
        /// username/password in the saved credentials (a refresh-token-only credential set has no
        /// password to construct an unauthenticated Session with).
        /// </summary>
        public static Session CreateSessionWithoutAuth()
        {
            var auth = CredentialResolver.Resolve(null, null, null);
            if (auth?.UserName == null || auth.Password == null)
            {
                throw new InvalidOperationException(
                    $"Ring API username/password not found at {CredentialResolver.AuthPath}.\n{SetupPointer}");
            }

            return new Session(auth.UserName, auth.Password);
        }

        /// <summary>
        /// Checks if credentials (a refresh token, or username/password) are available for real
        /// integration testing.
        /// </summary>
        public static bool CredentialsAvailable() => CredentialResolver.Resolve(null, null, null) != null;

        /// <summary>
        /// Gets a message explaining how to setup credentials, for tests that want to surface it directly.
        /// </summary>
        public static string GetSetupInstructions() =>
            $"Ring API credentials not found or not usable at {CredentialResolver.AuthPath}.\n{SetupPointer}";
    }
}

