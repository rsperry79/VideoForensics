using System;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring;

using VideoForensics.Providers.Ring.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Real Ring API integration tests using credentials from RingVideos app config.
    /// These tests require the RingVideos application to have been run with valid Ring API credentials.
    ///
    /// To setup:
    /// 1. Run the RingVideos application: dotnet run --project RingVideos/RingVideos.csproj
    /// 2. Enter your Ring API email and password when prompted
    /// 3. Credentials are automatically saved encrypted in AppData
    /// 4. Run these tests again to execute against real API
    ///
    /// These tests validate actual Ring API behavior and can be run in CI/CD
    /// if credentials are securely provided to the RingVideos app.
    ///
    /// Phase 4 Implementation - Uses RingVideos App Config
    /// </summary>
    public class RealIntegrationTests : IAsyncLifetime
    {
        private Session? _session;
        private bool _credentialsAvailable;

        public async ValueTask InitializeAsync()
        {
            _credentialsAvailable = RealSessionHelper.CredentialsAvailable();

            if (_credentialsAvailable)
            {
                try
                {
                    _session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
                }
                catch
                {
                    // If authentication fails, mark as unavailable
                    _credentialsAvailable = false;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            // Session cleanup if needed
            _session = null;
            return ValueTask.CompletedTask;
        }

        // ============================================================
        // Phase 4: Real Integration Tests
        // ============================================================

        [Fact]
        [Trait("Description", "Real API: Verifies session can be created with valid credentials")]
        public void RealSession_CanBeCreatedWithCredentials()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            // CreateSessionWithoutAuth specifically needs username/password - a refresh-token-only
            // credential set (the common case after the SelfTester's 2FA `--auth` flow) can't
            // satisfy it even though _credentialsAvailable is true.
            if (!RealSessionHelper.UsernamePasswordAvailable()) Assert.Skip("Saved credentials are refresh-token-only; this test needs username/password");

            // Arrange & Act
            var session = RealSessionHelper.CreateSessionWithoutAuth();

            // Assert
            Assert.NotNull(session);
            Assert.False(session.IsAuthenticated, "Session should not be authenticated until Authenticate() is called");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session authentication succeeds with valid credentials")]
        public async Task RealSession_CanAuthenticateWithValidCredentials()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var isAuthenticated = _session.IsAuthenticated;
            var authToken = _session.AuthenticationToken;

            // Assert
            Assert.True(isAuthenticated, "Session should be authenticated after successful Authenticate() call");
            Assert.False(string.IsNullOrEmpty(authToken), "Authentication token should be set");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session properties are accessible")]
        public void RealSession_PropertiesAreAccessible()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var username = _session.Username;
            var oauthUrl = _session.OAuthUrl;
            var baseUrl = _session.BaseUrl;

            // Assert
            // Username is only populated when the session was constructed from username/password
            // (see Session's constructors) - a refresh-token-only credential set (the common case
            // after the SelfTester's 2FA `--auth` flow) authenticates a session with no Username set.
            if (RealSessionHelper.UsernamePasswordAvailable())
            {
                Assert.NotNull(username);
            }
            Assert.NotNull(oauthUrl);
            Assert.NotNull(baseUrl);
            Assert.True(oauthUrl.ToString().Contains("oauth.ring.com"));
            Assert.True(baseUrl.ToString().Contains("api.ring.com"));
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetRingDevices works with authenticated session")]
        public async Task RealSession_CanGetRingDevices()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var devices = await _session.GetRingDevices();

            // Assert
            Assert.NotNull(devices);
            // Devices may be empty or populated, both are valid states
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetLocations works with authenticated session")]
        public async Task RealSession_CanGetLocations()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var locations = await _session.GetLocations();

            // Assert
            Assert.NotNull(locations);
            // Locations collection may be empty or populated
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetDoorbotsHistory works with authenticated session")]
        public async Task RealSession_CanGetDoorbotsHistory()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var history = await _session.GetDoorbotsHistory();

            // Assert
            Assert.NotNull(history);
            // History may be empty or populated depending on account activity
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session remains authenticated for multiple calls")]
        public async Task RealSession_RemainsAuthenticatedAcrossMultipleCalls()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var auth1 = _session.IsAuthenticated;
            await _session.GetRingDevices(); // Make API call
            var auth2 = _session.IsAuthenticated;
            await _session.GetLocations(); // Make another API call
            var auth3 = _session.IsAuthenticated;

            // Assert
            Assert.True(auth1 && auth2 && auth3, "Session should remain authenticated across multiple calls");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies authentication token persists")]
        public async Task RealSession_AuthenticationTokenPersists()
        {
            if (!_credentialsAvailable) Assert.Skip("Ring API credentials not configured in AppData");
            if (_session == null) Assert.Skip("Failed to create authenticated session");

            // Act
            var token1 = _session.AuthenticationToken;
            await _session.GetRingDevices();
            var token2 = _session.AuthenticationToken;

            // Assert
            Assert.NotNull(token1);
            Assert.Equal(token1, token2);
        }

        // ============================================================
        // Setup Instructions for Phase 4
        // ============================================================

        /// <summary>
        /// Call this method once to see setup instructions
        /// </summary>
        [Fact]
        [Trait("Description", "Displays instructions for setting up real integration tests")]
        public void PrintPhase4SetupInstructions()
        {
            var instructions = RealSessionHelper.GetSetupInstructions();
            Console.WriteLine(instructions);
        }
    }
}
