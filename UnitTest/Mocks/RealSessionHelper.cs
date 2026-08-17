using KoenZomers.Ring.Api;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoenZomers.Ring.UnitTest.Mocks
{
    /// <summary>
    /// Helper class for creating real Ring API sessions for integration testing.
    /// Uses the RingVideos application's AppData config for credentials.
    ///
    /// The RingVideos app stores encrypted credentials at:
    /// %APPDATA%/RingVideosData/RingVideosConfig.json
    ///
    /// To use:
    /// 1. Run RingVideos app and enter your Ring API credentials
    /// 2. Credentials are automatically saved in AppData (encrypted)
    /// 3. Tests will automatically use these credentials: var session = await RealSessionHelper.CreateAuthenticatedSessionAsync()
    /// 4. Run tests with real API
    /// </summary>
    public class RealSessionHelper
    {
        /// <summary>
        /// RingVideos app config structure (mirrors RingVideos.Models.Config)
        /// </summary>
        private class RingVideosConfig
        {
            public Authentication? Authentication { get; set; }
        }

        /// <summary>
        /// Authentication model (mirrors RingVideos.Models.Authentication)
        /// </summary>
        private class Authentication
        {
            public string? UserName { get; set; }
            public string? Password { get; set; }
            public string? RefreshToken { get; set; }
            public string? EncryptionIV { get; set; }
        }

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RingVideosData",
            "RingVideosConfig.json"
        );

        /// <summary>
        /// Creates and authenticates a session using credentials from RingVideos AppData config.
        /// Throws InvalidOperationException if credentials not found.
        /// </summary>
        public static async Task<Api.Session> CreateAuthenticatedSessionAsync()
        {
            var auth = LoadAuthenticationFromAppData();
            if (auth?.UserName == null || auth.Password == null)
            {
                throw new InvalidOperationException(
                    $"Ring API credentials not found in RingVideos config. " +
                    $"Run the RingVideos application first and enter your Ring API credentials.\n" +
                    $"Config location: {ConfigPath}\n\n" +
                    GetSetupInstructions()
                );
            }

            var session = new Api.Session(auth.UserName, auth.Password);

            try
            {
                await session.Authenticate();
                return session;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to authenticate with Ring API using RingVideos app credentials. " +
                    $"Verify your Ring API email/password are correct in the app. Error: {ex.Message}", ex
                );
            }
        }

        /// <summary>
        /// Creates a session without authenticating (for testing session creation only).
        /// Uses credentials from RingVideos AppData config but doesn't call Authenticate().
        /// </summary>
        public static Api.Session CreateSessionWithoutAuth()
        {
            var auth = LoadAuthenticationFromAppData();
            if (auth?.UserName == null || auth.Password == null)
            {
                throw new InvalidOperationException(
                    $"Ring API credentials not found in RingVideos config. " +
                    $"Run the RingVideos application first and enter your Ring API credentials."
                );
            }

            return new Api.Session(auth.UserName, auth.Password);
        }

        /// <summary>
        /// Checks if credentials are available for real integration testing.
        /// </summary>
        public static bool CredentialsAvailable()
        {
            try
            {
                var auth = LoadAuthenticationFromAppData();
                return !string.IsNullOrEmpty(auth?.UserName) && !string.IsNullOrEmpty(auth?.Password);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loads authentication from RingVideos AppData config
        /// </summary>
        private static Authentication? LoadAuthenticationFromAppData()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return null;
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<RingVideosConfig>(json);
                return config?.Authentication;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a message explaining how to setup credentials
        /// </summary>
        public static string GetSetupInstructions()
        {
            return $@"
To enable real integration tests:

1. Run the RingVideos application:
   dotnet run --project RingVideos/RingVideos.csproj

2. Enter your Ring API email and password when prompted
   The app will save encrypted credentials to AppData

3. Credentials are automatically stored at:
   {ConfigPath}

4. Real integration tests will then use these credentials:
   var session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
   var devices = await session.GetRingDevices();

The Ring API credentials are encrypted and tied to this machine.
They are safe to store in AppData.
";
        }
    }
}
