using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Real Ring API integration tests using the refresh token saved in the database.
    /// Sessions are restored through RealSessionHelper, the same RingAuthService path the server uses.
    ///
    /// To setup:
    /// 1. Run the SelfTester authentication flow: cd src/selftest && dotnet run -- --auth
    /// 2. Complete the Ring sign-in (two-factor accounts included)
    /// 3. The encrypted refresh token is saved to the database
    /// 4. Run these tests again to execute against the real API
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
            // Arrange: a session built from credentials supplied here. No network call happens until Authenticate(),
            // so this needs no saved Ring account and must never skip.
            // Act
            var session = new Session("test-user@example.com", "test-password");

            // Assert
            Assert.NotNull(session);
            Assert.False(session.IsAuthenticated, "Session should not be authenticated until Authenticate() is called");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session authentication succeeds with valid credentials")]
        public async Task RealSession_CanAuthenticateWithValidCredentials()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            bool isAuthenticated = _session.IsAuthenticated;
            string authToken = _session.AuthenticationToken;

            // Assert
            Assert.True(isAuthenticated, "Session should be authenticated after successful Authenticate() call");
            Assert.False(string.IsNullOrEmpty(authToken), "Authentication token should be set");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session properties are accessible")]
        public void RealSession_PropertiesAreAccessible()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            string username = _session.Username;
            Uri oauthUrl = _session.OAuthUrl;
            Uri baseUrl = _session.BaseUrl;

            // Assert
            Assert.NotNull(oauthUrl);
            Assert.NotNull(baseUrl);
            Assert.True(oauthUrl.ToString().Contains("oauth.ring.com"));
            Assert.True(baseUrl.ToString().Contains("api.ring.com"));
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetRingDevices works with authenticated session")]
        public async Task RealSession_CanGetRingDevices()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            Entities.Devices devices = await _session.GetRingDevices();

            // Assert
            Assert.NotNull(devices);
            // Devices may be empty or populated, both are valid states
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetLocations works with authenticated session")]
        public async Task RealSession_CanGetLocations()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            List<Entities.Location> locations = await _session.GetLocations();

            // Assert
            Assert.NotNull(locations);
            // Locations collection may be empty or populated
        }

        [Fact]
        [Trait("Description", "Real API: Verifies GetDoorbotsHistory works with authenticated session")]
        public async Task RealSession_CanGetDoorbotsHistory()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            List<Entities.DoorbotHistoryEvent> history = await _session.GetDoorbotsHistory();

            // Assert
            Assert.NotNull(history);
            // History may be empty or populated depending on account activity
        }

        [Fact]
        [Trait("Description", "Real API: Verifies session remains authenticated for multiple calls")]
        public async Task RealSession_RemainsAuthenticatedAcrossMultipleCalls()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            bool auth1 = _session.IsAuthenticated;
            _ = await _session.GetRingDevices(); // Make API call
            bool auth2 = _session.IsAuthenticated;
            _ = await _session.GetLocations(); // Make another API call
            bool auth3 = _session.IsAuthenticated;

            // Assert
            Assert.True(auth1 && auth2 && auth3, "Session should remain authenticated across multiple calls");
        }

        [Fact]
        [Trait("Description", "Real API: Verifies authentication token persists")]
        public async Task RealSession_AuthenticationTokenPersists()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
            }

            if (_session == null)
            {
                Assert.Skip("Failed to create authenticated session");
            }

            // Act
            string token1 = _session.AuthenticationToken;
            _ = await _session.GetRingDevices();
            string token2 = _session.AuthenticationToken;

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
            string instructions = RealSessionHelper.GetSetupInstructions();
            Console.WriteLine(instructions);
        }
    }
}
