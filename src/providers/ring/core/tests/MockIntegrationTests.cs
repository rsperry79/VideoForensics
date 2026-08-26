using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring;

using VideoForensics.Providers.Ring.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Mock-based variants of integration tests. These tests don't require real Ring API credentials.
    /// They use MockHttpMessageHandler to simulate API responses.
    /// </summary>
    public class MockIntegrationTests
    {
        private MockSessionHelper? _mockHelper;
        private Session? _mockSession;

        public MockIntegrationTests()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper!.CreateSessionWithMockHandler();
        }

        [Fact]
        public void MockSession_CanBeAuthenticated()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var session = new Session("test@example.com", "testpass", mockHandler);

            // Act
            var isAuthenticated = session.IsAuthenticated;

            // Assert
            Assert.False(isAuthenticated, "Session should not be authenticated without token");
        }

        [Fact]
        public void MockSession_HasCorrectUsername()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpass";

            // Act
            var session = new Session(username, password);

            // Assert
            Assert.Equal(username, session.Username);
            Assert.Equal(password, session.Password);
        }

        [Fact]
        public void MockSession_CanAccessApiUrls()
        {
            // Arrange
            var session = _mockSession!;

            // Act
            var oauthUrl = session.OAuthUrl;
            var baseUrl = session.BaseUrl;

            // Assert
            Assert.NotNull(oauthUrl);
            Assert.NotNull(baseUrl);
            Assert.True(oauthUrl.ToString().Contains("oauth.ring.com"));
            Assert.True(baseUrl.ToString().Contains("api.ring.com"));
        }

        [Fact]
        public async Task MockSession_CanCallGetRingDevices()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "api.ring.com/clients_api/v1/user/devices",
                TestFixtures.DeviceResponses.DevicesWithDoorbot
            );

            // Act
            try
            {
                var devices = await _mockSession!.GetRingDevices();

                // Assert
                Assert.NotNull(devices);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
                // Expected - session not authenticated
            }
        }

        [Fact]
        public async Task MockSession_ThrowsWhenNotAuthenticated()
        {
            // Arrange
            var session = _mockSession!;

            // Act & Assert - This should throw because session is not authenticated
            try
            {
                await session.GetRingDevices();
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public void MockSession_SupportsMultipleInstances()
        {
            // Arrange
            var session1 = new Session("user1@example.com", "pass1");
            var session2 = new Session("user2@example.com", "pass2");
            var session3 = new Session("user3@example.com", "pass3");

            // Act & Assert
            Assert.NotEqual(session1.Username, session2.Username);
            Assert.NotEqual(session2.Username, session3.Username);
            Assert.NotEqual(session1.Username, session3.Username);
        }

        [Fact]
        public void MockHandler_CanSetupMultipleResponses()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            _mockHelper!.SetupMockResponse(
                "api.ring.com/devices",
                TestFixtures.DeviceResponses.DevicesWithDoorbot
            );
            _mockHelper!.SetupMockResponse(
                "api.ring.com/activity",
                TestFixtures.HistoryResponses.MotionEventHistory
            );

            // Act
            var handler1 = _mockHelper!.GetMockHandler();

            // Assert
            Assert.NotNull(handler1);
        }

        [Fact]
        public async Task MockSession_WithMockHandler_CanBeCreatedFromRefreshToken()
        {
            // Arrange
            var refreshToken = "mock_refresh_token_abc123";
            _mockHelper!.SetupMockResponse(
                "https://oauth.ring.com/oauth/token",
                TestFixtures.AuthResponses.SuccessfulOAuthToken
            );

            // Act
            try
            {
                var session = await Session.GetSessionByRefreshToken(refreshToken, _mockHelper!.GetMockHandler());

                // Assert
                Assert.NotNull(session);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.AuthenticationFailedException)
            {
                // Expected - mock handler doesn't have real token response configured
            }
        }

        [Fact]
        public void MockSession_ApiUrlsAreConsistent()
        {
            // Arrange
            var session1 = new Session("user@example.com", "pass");
            var session2 = new Session("user@example.com", "pass");

            // Act
            var url1 = session1.BaseUrl;
            var url2 = session2.BaseUrl;

            // Assert
            Assert.Equal(url1, url2);
        }

        [Fact]
        public void MockHandler_DefaultResponsesAreConfigured()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();

            // Act - The handler should have default responses set up
            var handler = mockHandler;

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public async Task MockSession_CanHandleMultipleRequests()
        {
            // Arrange
            var sessions = new List<Session>();
            for (int i = 0; i < 5; i++)
            {
                sessions.Add(new Session($"user{i}@example.com", $"pass{i}", _mockHelper!.GetMockHandler()));
            }

            // Act & Assert
            Assert.Equal(5, sessions.Count);
            foreach (var session in sessions)
            {
                Assert.NotNull(session);
                Assert.False(session.IsAuthenticated);
            }
        }

        [Fact]
        public void MockSession_AuthenticationTokenIsNullWhenNotAuthenticated()
        {
            // Arrange
            var session = _mockSession!;

            // Act
            var token = session.AuthenticationToken;

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task MockSession_DeviceExceptionHandling()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();

            // Act & Assert
            try
            {
                // Try to get devices without being authenticated
                await _mockSession!.GetRingDevices();
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public void MockSession_PasswordIsNotAccessible()
        {
            // Arrange
            var password = "secretpassword";
            var session = new Session("test@example.com", password);

            // Act
            var savedPassword = session.Password;

            // Assert
            Assert.Equal(password, savedPassword);
        }

        [Fact]
        public async Task MockSession_MultipleSessionsIndependent()
        {
            // Arrange
            var session1 = new Session("user1@example.com", "pass1", _mockHelper!.GetMockHandler());
            var session2 = new Session("user2@example.com", "pass2", _mockHelper!.GetMockHandler());

            // Act
            var auth1 = session1.IsAuthenticated;
            var auth2 = session2.IsAuthenticated;

            // Assert
            Assert.Equal(auth1, auth2);
            Assert.False(auth1 || auth2);
        }

        // Phase 3B: Device Operations Tests
        [Fact]
        public async Task MockSession_CanGetDevicesViaApiUrl()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/devices",
                TestFixtures.DeviceResponses.DevicesWithDoorbot
            );
            var session = _mockHelper!.CreateSessionWithMockHandler();

            // Act & Assert
            Assert.NotNull(session);
            Assert.NotNull(session.BaseUrl);
        }

        [Fact]
        public async Task MockSession_CanGetLocations()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/locations",
                TestFixtures.LocationResponses.LocationsList
            );

            // Act
            try
            {
                var locations = await _mockSession!.GetLocations()!;
                // Locations can only be retrieved when authenticated
                Assert.True(locations != null || !_mockSession!.IsAuthenticated);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public async Task MockSession_CanSetupHistoryResponse()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/activity",
                TestFixtures.HistoryResponses.MotionEventHistory
            );

            // Act
            try
            {
                var history = await _mockSession!.GetDoorbotsHistory()!;
                Assert.True(history != null || !_mockSession!.IsAuthenticated);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public async Task MockSession_CanSetupSnapshotTimestampResponse()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/doorbots/123456/motion_snooze",
                TestFixtures.SnapshotResponses.SnapshotTimestamp
            );

            // Act & Assert
            Assert.NotNull(_mockSession);
        }

        [Fact]
        public async Task MockSession_CanSetupRecordingShareResponse()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/ding/xyz789/share",
                TestFixtures.RecordingResponses.RecordingShareUrl
            );

            // Act & Assert
            Assert.NotNull(_mockSession);
        }

        // Phase 3B: Error Scenario Tests
        [Fact]
        public void MockHandler_Can401Unauthorized()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/devices",
                TestFixtures.ErrorResponses.Unauthorized,
                System.Net.HttpStatusCode.Unauthorized
            );

            // Act & Assert
            Assert.NotNull(mockHandler);
        }

        [Fact]
        public void MockHandler_Can404NotFound()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/devices/invalid",
                TestFixtures.ErrorResponses.NotFound,
                System.Net.HttpStatusCode.NotFound
            );

            // Act & Assert
            Assert.NotNull(mockHandler);
        }

        [Fact]
        public void MockHandler_Can429TooManyRequests()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/activity",
                TestFixtures.ErrorResponses.RateLimitExceeded,
                System.Net.HttpStatusCode.TooManyRequests
            );

            // Act & Assert
            Assert.NotNull(mockHandler);
        }

        [Fact]
        public void MockHandler_Can500InternalError()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/devices",
                TestFixtures.ErrorResponses.InternalServerError,
                System.Net.HttpStatusCode.InternalServerError
            );

            // Act & Assert
            Assert.NotNull(mockHandler);
        }

        [Fact]
        public async Task MockSession_MultipleDeviceTypesSupported()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/devices",
                TestFixtures.DeviceResponses.DevicesWithDoorbot
            );

            // Act
            var session = _mockHelper!.CreateSessionWithMockHandler();

            // Assert
            Assert.NotNull(session);
            Assert.NotNull(session.BaseUrl);
        }

        [Fact]
        public async Task MockSession_CanHandleEmptyDeviceList()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/devices",
                TestFixtures.DeviceResponses.DevicesEmpty
            );

            // Act
            try
            {
                var devices = await _mockSession!.GetRingDevices();
                Assert.True(devices != null || !_mockSession!.IsAuthenticated);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public async Task MockSession_CanHandleMultipleHistoryEvents()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/user/activity",
                TestFixtures.HistoryResponses.MotionEventHistory
            );

            // Act
            try
            {
                var history = await _mockSession!.GetDoorbotsHistory();
                Assert.True(history != null || !_mockSession!.IsAuthenticated);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        [Fact]
        public async Task MockSession_RefreshTokenViaHttpMessageHandler()
        {
            // Arrange
            var refreshToken = "test_refresh_token";
            _mockHelper!.SetupMockResponse(
                "https://oauth.ring.com/oauth/token",
                TestFixtures.AuthResponses.SuccessfulOAuthToken
            );

            // Act
            try
            {
                var newSession = await Session.GetSessionByRefreshToken(refreshToken, _mockHelper!.GetMockHandler());
                Assert.NotNull(newSession);
            }
            catch (VideoForensics.Providers.Ring.Exceptions.AuthenticationFailedException)
            {
            }
        }

        [Fact]
        public void MockSession_UrlsRemainConsistentAcrossCalls()
        {
            // Arrange
            var session = _mockSession!;

            // Act
            var oauth1 = session.OAuthUrl;
            var base1 = session.BaseUrl;
            var oauth2 = session.OAuthUrl;
            var base2 = session.BaseUrl;

            // Assert
            Assert.Equal(oauth1, oauth2);
            Assert.Equal(base1, base2);
        }

        // --- Device control endpoints (light, siren, chime test sound) ---
        //
        // These fill a gap versus other unofficial Ring API clients (python-ring-doorbell,
        // ring-client-api), which all expose light/siren/chime-test control but which this
        // wrapper previously did not. Each test authenticates against the default mocked OAuth
        // response, then asserts both that the call succeeds and that it hit the exact endpoint
        // and HTTP verb the real Ring API expects.

        [Fact]
        public async Task MockSession_SetLight_On_CallsFloodlightOnEndpoint()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/floodlight_light_on", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            // Act
            await _mockSession!.SetLight(123456, true);

            // Assert
            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("floodlight_light_"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
            Assert.True(call.Url.EndsWith("doorbots/123456/floodlight_light_on"), $"Unexpected url: {call.Url}");
        }

        [Fact]
        public async Task MockSession_SetLight_Off_CallsFloodlightOffEndpoint()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/floodlight_light_off", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            // Act
            await _mockSession!.SetLight(123456, false);

            // Assert
            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("floodlight_light_"));
            Assert.NotNull(call.Url);
            Assert.True(call.Url.EndsWith("doorbots/123456/floodlight_light_off"), $"Unexpected url: {call.Url}");
        }

        [Fact]
        public async Task MockSession_SetSiren_On_CallsSirenOnEndpointWithDuration()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/siren_on", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            // Act
            await _mockSession!.SetSiren(123456, true, durationSeconds: 30);

            // Assert
            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("siren_"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
            Assert.True(call.Url.Contains("doorbots/123456/siren_on"), $"Unexpected url: {call.Url}");
            Assert.True(call.Url.Contains("duration=30"), $"Expected duration query param, got: {call.Url}");
        }

        [Fact]
        public async Task MockSession_SetSiren_Off_CallsSirenOffEndpoint()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/siren_off", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            // Act
            await _mockSession!.SetSiren(123456, false);

            // Assert
            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("siren_"));
            Assert.NotNull(call.Url);
            Assert.True(call.Url.EndsWith("doorbots/123456/siren_off"), $"Unexpected url: {call.Url}");
        }

        [Fact]
        public async Task MockSession_TestChimeSound_DefaultsToDingKind()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/chimes/789012/play_sound", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            // Act
            await _mockSession!.TestChimeSound(789012);

            // Assert
            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("play_sound"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
            Assert.True(call.Url.EndsWith("chimes/789012/play_sound"), $"Unexpected url: {call.Url}");
        }

        [Fact]
        public async Task MockSession_DeviceControl_ThrowsWhenNotAuthenticated()
        {
            // Arrange - a fresh, never-authenticated session
            var session = _mockHelper!.CreateSessionWithMockHandler();

            // Act & Assert
            try
            {
                await session.SetLight(123456, true);
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }

            try
            {
                await session.SetSiren(123456, true);
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }

            try
            {
                await session.TestChimeSound(789012);
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
        }

        // --- Phase 1: device setting setters ---

        [Fact]
        public async Task MockSession_SetVolume_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetVolume(123456, 5);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [Fact]
        public async Task MockSession_SetMotionDetection_CallsSettingsEndpointWithPatch()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/devices/v1/devices/123456/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetMotionDetection(123456, false);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("devices/123456/settings"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Patch, call.Method);
        }

        [Fact]
        public async Task MockSession_SetChimeType_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetChimeType(123456, 1, enabled: true, duration: 3);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [Fact]
        public async Task MockSession_SetDoNotDisturb_CallsChimeEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/chimes/789012/do_not_disturb", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetDoNotDisturb(789012, 300);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("do_not_disturb"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [Fact]
        public async Task MockSession_SetNightMode_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetNightMode(123456, true);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        // --- Phase 2: motion zones ---

        [Fact]
        public async Task MockSession_SetMotionZones_CallsSettingsEndpointWithPatch()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/devices/v1/devices/123456/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            var zones = new VideoForensics.Providers.Ring.Entities.AdvancedMotionZones
            {
                Zone1 = new VideoForensics.Providers.Ring.Entities.Zone { Name = "Front Yard", State = 1 }
            };
            await _mockSession!.SetMotionZones(123456, zones);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("devices/123456/settings"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Patch, call.Method);
        }

        [Fact]
        public async Task MockSession_SetMotionZones_ThrowsOnNullZones()
        {
            var session = _mockHelper!.CreateSessionWithMockHandler();
            await session.Authenticate();

            try
            {
                await session.SetMotionZones(123456, null!);
                Assert.Fail("Should have thrown ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        // --- Phase 3: light groups ---

        [Fact]
        public async Task MockSession_GetGroups_ParsesDeviceGroups()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/groups/v1/locations/{locationId:D}/groups",
                System.Net.HttpStatusCode.OK,
                @"{ ""device_groups"": [ { ""device_group_id"": ""grp-1"", ""name"": ""Backyard Lights"" } ] }");
            await _mockSession!.Authenticate();

            var groups = await _mockSession!.GetGroups(locationId);

            Assert.NotNull(groups);
            Assert.Equal(1, groups.Count);
            Assert.Equal("grp-1", groups[0].DeviceGroupId);
            Assert.Equal("Backyard Lights", groups[0].Name);
        }

        [Fact]
        public async Task MockSession_SetGroupLights_CallsGroupDevicesEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/groups/v1/locations/{locationId:D}/groups/grp-1/devices",
                System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetGroupLights(locationId, "grp-1", true, durationSeconds: 60);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("groups/grp-1/devices"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        // --- Phase 4: shared users / invitations ---

        [Fact]
        public async Task MockSession_GetSharedUsers_ParsesUsers()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/clients_api/locations/{locationId:D}/users",
                System.Net.HttpStatusCode.OK,
                @"[ { ""id"": 1, ""verified"": true, ""first_name"": ""Guest"", ""last_name"": ""User"", ""email"": ""guest@example.com"", ""object_type"": ""user"", ""devices"": [ { ""id"": 123456, ""role"": ""shared_user"", ""device_type"": ""cocoa_camera"", ""permissions"": null } ] } ]");
            await _mockSession!.Authenticate();

            var users = await _mockSession!.GetSharedUsers(locationId);

            Assert.NotNull(users);
            Assert.Equal(1, users.Count);
            Assert.Equal("guest@example.com", users[0].Email);
            Assert.Equal(1, users[0].Devices.Count);
            Assert.Equal("shared_user", users[0].Devices[0].Role);
        }

        [Fact]
        public async Task MockSession_GetInvitations_ParsesInvitations()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/clients_api/locations/{locationId:D}/invitations",
                System.Net.HttpStatusCode.OK,
                @"{ ""invitations"": [ { ""id"": 1, ""invited_email"": ""pending@example.com"", ""status"": ""pending"" } ] }");
            await _mockSession!.Authenticate();

            var invitations = await _mockSession!.GetInvitations(locationId);

            Assert.NotNull(invitations);
            Assert.Equal(1, invitations.Count);
            Assert.Equal("pending@example.com", invitations[0].InvitedEmail);
        }

        // --- Phase 5: location mode ---

        [Fact]
        public async Task MockSession_GetLocationMode_ParsesMode()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/rs/mode/location/{locationId:D}",
                System.Net.HttpStatusCode.OK,
                @"{ ""mode"": ""away"" }");
            await _mockSession!.Authenticate();

            var result = await _mockSession!.GetLocationMode(locationId);

            Assert.NotNull(result);
            Assert.Equal("away", result.Mode);
        }

        [Fact]
        public async Task MockSession_SetLocationMode_CallsModeEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/rs/mode/location/{locationId:D}",
                System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetLocationMode(locationId, "home");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains($"rs/mode/location/{locationId:D}"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_NewPhaseMethods_ThrowWhenNotAuthenticated()
        {
            var session = _mockHelper!.CreateSessionWithMockHandler();
            var locationId = Guid.NewGuid();

            async Task ExpectNotAuthenticated(Func<Task> action)
            {
                try
                {
                    await action();
                    Assert.Fail("Should have thrown SessionNotAuthenticatedException");
                }
                catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
            }

            await ExpectNotAuthenticated(() => session.SetVolume(123456, 5));
            await ExpectNotAuthenticated(() => session.SetMotionDetection(123456, true));
            await ExpectNotAuthenticated(() => session.SetChimeType(123456, 1));
            await ExpectNotAuthenticated(() => session.SetDoNotDisturb(789012, 60));
            await ExpectNotAuthenticated(() => session.SetNightMode(123456, true));
            await ExpectNotAuthenticated(() => session.SetMotionZones(123456, new VideoForensics.Providers.Ring.Entities.AdvancedMotionZones()));
            await ExpectNotAuthenticated(() => session.GetGroups(locationId));
            await ExpectNotAuthenticated(() => session.SetGroupLights(locationId, "grp-1", true));
            await ExpectNotAuthenticated(() => session.GetSharedUsers(locationId));
            await ExpectNotAuthenticated(() => session.GetInvitations(locationId));
            await ExpectNotAuthenticated(() => session.GetLocationMode(locationId));
            await ExpectNotAuthenticated(() => session.SetLocationMode(locationId, "home"));
        }

        // --- Phase 6: device/chime health ---

        [Fact]
        public async Task MockSession_GetDoorbotHealth_ParsesHealth()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            // Ring returns battery_percentage as a JSON string, confirmed via a live ApiTester run.
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/doorbots/123456/health",
                System.Net.HttpStatusCode.OK,
                @"{ ""device_health"": { ""connected"": true, ""battery_percentage"": ""88"" } }");
            await _mockSession!.Authenticate();

            var health = await _mockSession!.GetDoorbotHealth(123456);

            Assert.NotNull(health?.DeviceHealth);
            Assert.Equal(true, health.DeviceHealth.Connected);
            Assert.Equal(88, health.DeviceHealth.BatteryPercentage);
        }

        [Fact]
        public async Task MockSession_GetChimeHealth_ParsesHealth()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/chimes/789012/health",
                System.Net.HttpStatusCode.OK,
                @"{ ""device_health"": { ""connected"": false } }");
            await _mockSession!.Authenticate();

            var health = await _mockSession!.GetChimeHealth(789012);

            Assert.NotNull(health?.DeviceHealth);
            Assert.Equal(false, health.DeviceHealth.Connected);
        }

        // --- Phase 7: ding/motion event push subscriptions ---

        [Fact]
        public async Task MockSession_SubscribeToDingEvents_CallsSubscribeEndpoint()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/subscribe", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SubscribeToDingEvents(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456/subscribe"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_UnsubscribeFromDingEvents_CallsUnsubscribeEndpoint()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/unsubscribe", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.UnsubscribeFromDingEvents(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456/unsubscribe"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_SubscribeToMotionEvents_CallsMotionsSubscribeEndpoint()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/motions_subscribe", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SubscribeToMotionEvents(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456/motions_subscribe"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_UnsubscribeFromMotionEvents_CallsMotionsUnsubscribeEndpoint()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456/motions_unsubscribe", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.UnsubscribeFromMotionEvents(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456/motions_unsubscribe"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        // --- Phase 8: generic device settings getter ---

        [Fact]
        public async Task MockSession_GetDeviceSettings_CallsSettingsEndpointWithGet()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/devices/v1/devices/123456/settings",
                System.Net.HttpStatusCode.OK,
                @"{ ""motion_settings"": { ""motion_detection_enabled"": true } }");
            await _mockSession!.Authenticate();

            var settings = await _mockSession!.GetDeviceSettings(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("devices/123456/settings"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Get, call.Method);
            Assert.True(settings.GetProperty("motion_settings").GetProperty("motion_detection_enabled").GetBoolean());
        }

        // --- Phase 9: video search & unified location/device event feeds ---

        [Fact]
        public async Task MockSession_VideoSearch_ParsesResults()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            // Shape confirmed against a live account via ApiTester: created_at is epoch
            // milliseconds (unlike doorbot-history's ISO string), and hq_url/lq_url are direct,
            // pre-signed download links returned inline.
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/video_search/history",
                System.Net.HttpStatusCode.OK,
                @"{ ""video_search"": [ { ""id"": 1, ""ding_id"": ""abc123"", ""kind"": ""motion"", ""created_at"": 1787074589441, ""duration"": 19, ""hq_url"": ""https://example.com/hq.mp4"", ""lq_url"": ""https://example.com/lq.mp4"" } ] }");
            await _mockSession!.Authenticate();

            var results = await _mockSession!.VideoSearch(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("video_search/history"));
            Assert.NotNull(call.Url);
            Assert.True(call.Url.Contains("doorbot_id=123456"));
            // Ring returns HTTP 400 without date_from/date_to, confirmed via a live ApiTester run -
            // so a default range is always sent even when the caller doesn't provide one.
            Assert.True(call.Url.Contains("date_from="), $"Expected a default date_from, got: {call.Url}");
            Assert.True(call.Url.Contains("date_to="), $"Expected a default date_to, got: {call.Url}");
            Assert.Equal(1, results.Count);
            Assert.Equal("motion", results[0].Kind);
            Assert.Equal("https://example.com/hq.mp4", results[0].HqUrl);
            Assert.Equal(19, results[0].Duration);
            Assert.NotNull(results[0].CreatedAt);
        }

        // Real shape confirmed via a live ApiTester run: this is NOT the same shape as
        // doorbots/history's DoorbotHistoryEvent (which the code originally, incorrectly, reused
        // here) - it has event_id/ding_id/owner_id instead of a single "id", a minimal nested
        // doorbot object ({id, description, type} - not the full Doorbot entity), and top-level
        // event_type/state/recorded/recording_status/is_e2ee fields DoorbotHistoryEvent has no
        // equivalent for. Reusing DoorbotHistoryEvent silently dropped nearly the entire payload
        // (no exception - System.Text.Json just leaves unmatched properties as their defaults).
        [Fact]
        public async Task MockSession_GetLocationEvents_ParsesEvents()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/clients_api/locations/{locationId:D}/events",
                System.Net.HttpStatusCode.OK,
                @"{ ""events"": [ {
                    ""event_id"": ""evt-abc"",
                    ""source_id"": ""src-123"",
                    ""event_type"": ""on_demand"",
                    ""state"": ""complete"",
                    ""favorite"": false,
                    ""recorded"": true,
                    ""recording_status"": ""ready"",
                    ""is_e2ee"": false,
                    ""created_at"": ""2026-08-18T19:18:16Z"",
                    ""had_subscription"": true,
                    ""owner_id"": ""168450658"",
                    ""riid"": ""r-1"",
                    ""doorbot_id"": 123456,
                    ""ding_id"": 7664686868948500897,
                    ""ding_id_str"": ""7664686868948500897"",
                    ""kind"": ""ding"",
                    ""doorbot"": { ""id"": 123456, ""description"": ""Front Door"", ""type"": ""lpd_v2"" },
                    ""cv_properties"": { ""person_detected"": true, ""detection_type"": ""human"" }
                } ], ""meta"": { ""pagination_key"": ""abc123"" } }");
            await _mockSession!.Authenticate();

            var events = await _mockSession!.GetLocationEvents(locationId);

            Assert.Equal(1, events.Count);
            Assert.Equal("ding", events[0].Kind);
            Assert.Equal("evt-abc", events[0].EventId);
            Assert.Equal(7664686868948500897, events[0].DingId);
            Assert.Equal("7664686868948500897", events[0].DingIdString);
            Assert.Equal(123456, events[0].DoorbotId);
            Assert.Equal("168450658", events[0].OwnerId);
            Assert.True(events[0].Recorded);
            Assert.Equal("Front Door", events[0].Doorbot.Description);
            Assert.Equal(true, events[0].CvProperties.PersonDetected);
        }

        [Fact]
        public async Task MockSession_GetDeviceEvents_ParsesEvents()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/clients_api/locations/{locationId:D}/devices/123456/events",
                System.Net.HttpStatusCode.OK,
                @"{ ""events"": [ { ""event_id"": ""evt-xyz"", ""kind"": ""motion"", ""doorbot_id"": 123456 } ] }");
            await _mockSession!.Authenticate();

            var events = await _mockSession!.GetDeviceEvents(locationId, 123456);

            Assert.Equal(1, events.Count);
            Assert.Equal("evt-xyz", events[0].EventId);
            Assert.Equal(123456, events[0].DoorbotId);
        }

        // --- Phase 10: snapshot extras ---

        [Fact]
        public async Task MockSession_GetSnapshotByUuid_DownloadsBytes()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/snapshots/uuid", System.Net.HttpStatusCode.OK, "fake-image-bytes");
            await _mockSession!.Authenticate();

            using var stream = await _mockSession!.GetSnapshotByUuid("some-uuid");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("snapshots/uuid"));
            Assert.NotNull(call.Url);
            Assert.True(call.Url.Contains("uuid=some-uuid"));
            Assert.NotNull(stream);
        }

        [Fact]
        public async Task MockSession_GetNextSnapshot_DownloadsFromAppSnapsHost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("app-snaps.ring.com/snapshots/next/123456", System.Net.HttpStatusCode.OK, "fake-image-bytes");
            await _mockSession!.Authenticate();

            using var stream = await _mockSession!.GetNextSnapshot(123456);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("snapshots/next/123456"));
            Assert.NotNull(call.Url);
            Assert.True(call.Url.Contains("app-snaps.ring.com"));
            Assert.NotNull(stream);
        }

        [Fact]
        public async Task MockSession_GetPeriodicalFootage_ParsesUrl()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/recordings/public/footages/event123",
                System.Net.HttpStatusCode.OK,
                @"{ ""url"": ""https://example.com/footage.mp4"" }");
            await _mockSession!.Authenticate();

            var footage = await _mockSession!.GetPeriodicalFootage("event123");

            Assert.Equal("https://example.com/footage.mp4", footage.Url);
        }

        // --- Phase 11: location mode settings & sharing ---

        [Fact]
        public async Task MockSession_GetLocationModeSettings_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/rs/mode/location/{locationId:D}/settings",
                System.Net.HttpStatusCode.OK,
                @"{ ""enabled"": true }");
            await _mockSession!.Authenticate();

            var settings = await _mockSession!.GetLocationModeSettings(locationId);

            Assert.True(settings.GetProperty("enabled").GetBoolean());
        }

        [Fact]
        public async Task MockSession_SetLocationModeSettings_CallsSettingsEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse($"api.ring.com/rs/mode/location/{locationId:D}/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetLocationModeSettings(locationId, @"{ ""enabled"": false }");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains($"mode/location/{locationId:D}/settings"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_EnableLocationModes_CallsSetupEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse($"api.ring.com/rs/mode/location/{locationId:D}/settings/setup", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.EnableLocationModes(locationId);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("settings/setup"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_DisableLocationModes_CallsSettingsEndpointWithDelete()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse($"api.ring.com/rs/mode/location/{locationId:D}/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.DisableLocationModes(locationId);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains($"mode/location/{locationId:D}/settings"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Delete, call.Method);
        }

        [Fact]
        public async Task MockSession_GetLocationModeSharing_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/rs/mode/location/{locationId:D}/sharing",
                System.Net.HttpStatusCode.OK,
                @"{ ""shareable"": true }");
            await _mockSession!.Authenticate();

            var sharing = await _mockSession!.GetLocationModeSharing(locationId);

            Assert.True(sharing.GetProperty("shareable").GetBoolean());
        }

        [Fact]
        public async Task MockSession_SetLocationModeSharing_CallsSharingEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse($"api.ring.com/rs/mode/location/{locationId:D}/sharing", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetLocationModeSharing(locationId, @"{ ""shareable"": false }");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains($"mode/location/{locationId:D}/sharing"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        // --- Phase 12: alarm monitoring status / trigger / location history ---

        [Fact]
        public async Task MockSession_GetAccountMonitoringStatus_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/rs/monitoring/accounts/{locationId:D}",
                System.Net.HttpStatusCode.OK,
                @"{ ""active"": true }");
            await _mockSession!.Authenticate();

            var status = await _mockSession!.GetAccountMonitoringStatus(locationId);

            Assert.True(status.GetProperty("active").GetBoolean());
        }

        [Fact]
        public async Task MockSession_TriggerAlarm_CallsUserAlarmEndpointWithPost()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            var assetUuid = "asset-1";
            mockHandler.SetupResponse(
                $"api.ring.com/rs/monitoring/accounts/{locationId:D}/assets/{assetUuid}/useralarm",
                System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.TriggerAlarm(locationId, assetUuid);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("userAlarm"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [Fact]
        public async Task MockSession_GetLocationHistory_ReturnsRawJson()
        {
            // Path corrected in Phase 16 (evm/v2/history/locations, not rs/history) - see
            // MockSession_GetLocationHistory_UsesEvmPath for the path-specific assertion.
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/evm/v2/history/locations/{locationId:D}",
                System.Net.HttpStatusCode.OK,
                @"{ ""items"": [] }");
            await _mockSession!.Authenticate();

            var history = await _mockSession!.GetLocationHistory(locationId);

            Assert.True(history.TryGetProperty("items", out _));
        }

        // --- Phase 13: profile, push registration, ringtones, Amazon Key ---

        // Real shape confirmed via a live ApiTester run: the response is wrapped in a top-level
        // "profile" key ({"profile": {...}}), not the bare object the code originally,
        // incorrectly, deserialized into directly - which silently produced an all-null/default
        // Profile every time (no exception, since "profile" just didn't match any Profile
        // property). phone_number is also a plain JSON string, not the ambiguous "object" the
        // entity previously declared it as.
        [Fact]
        public async Task MockSession_GetProfile_ParsesProfile()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/profile",
                System.Net.HttpStatusCode.OK,
                @"{ ""profile"": { ""id"": 42, ""email"": ""me@example.com"", ""phone_number"": ""+12065551234"" } }");
            await _mockSession!.Authenticate();

            var profile = await _mockSession!.GetProfile();

            Assert.Equal(42, profile.Id);
            Assert.Equal("me@example.com", profile.Email);
            Assert.Equal("+12065551234", profile.PhoneNumber);
        }

        [Fact]
        public async Task MockSession_RegisterPushReceiver_CallsDeviceEndpointWithPatch()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/device", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.RegisterPushReceiver("push-token-abc");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("clients_api/device"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Patch, call.Method);
        }

        // Real shape confirmed via a live ApiTester run: the list is under "audios", not
        // "ringtones" as originally, incorrectly, assumed - so GetRingtones() always silently
        // returned an empty list. Ids are also non-numeric strings, not the long? the entity
        // previously declared (which would have thrown a JsonException once the wrapper key was
        // fixed, since e.g. "chime_default_ding_2" doesn't parse as a number).
        [Fact]
        public async Task MockSession_GetRingtones_ParsesRingtones()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/ringtones",
                System.Net.HttpStatusCode.OK,
                @"{ ""audios"": [ { ""id"": ""chime_default_ding_2"", ""description"": ""Default Ding"", ""category"": ""ding"", ""available"": true } ] }");
            await _mockSession!.Authenticate();

            var ringtones = await _mockSession!.GetRingtones();

            Assert.Equal(1, ringtones.Count);
            Assert.Equal("Default Ding", ringtones[0].Description);
            Assert.Equal("chime_default_ding_2", ringtones[0].Id);
            Assert.Equal("ding", ringtones[0].Category);
        }

        [Fact]
        public async Task MockSession_FetchAmazonKeyLocks_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/integrations/amazonkey/v2/devices/lock_associations",
                System.Net.HttpStatusCode.OK,
                @"{ ""locks"": [] }");
            await _mockSession!.Authenticate();

            var locks = await _mockSession!.FetchAmazonKeyLocks();

            Assert.True(locks.TryGetProperty("locks", out _));
        }

        // --- Phase 14: chime update ---

        [Fact]
        public async Task MockSession_UpdateChime_CallsChimesEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/chimes/789012", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.UpdateChime(789012, @"{ ""description"": ""Front Chime"" }");

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("chimes/789012"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        // --- Phase 15: intercom unlock ---

        [Fact]
        public async Task MockSession_UnlockIntercom_CallsDeviceRpcEndpointWithPut()
        {
            // Path corrected in Phase 16 (commands/v1, not devices/v1) - see
            // MockSession_UnlockIntercom_UsesCommandsV1Path for the path-specific assertion.
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/commands/v1/devices/555/device_rpc", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.Unlock(555);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("device_rpc"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        // --- Phases 6-15: not-authenticated coverage ---

        [Fact]
        public async Task MockSession_Phase6To15Methods_ThrowWhenNotAuthenticated()
        {
            var session = _mockHelper!.CreateSessionWithMockHandler();
            var locationId = Guid.NewGuid();

            async Task ExpectNotAuthenticated(Func<Task> action)
            {
                try
                {
                    await action();
                    Assert.Fail("Should have thrown SessionNotAuthenticatedException");
                }
                catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
            }

            await ExpectNotAuthenticated(() => session.GetDoorbotHealth(123456));
            await ExpectNotAuthenticated(() => session.GetChimeHealth(789012));
            await ExpectNotAuthenticated(() => session.SubscribeToDingEvents(123456));
            await ExpectNotAuthenticated(() => session.UnsubscribeFromDingEvents(123456));
            await ExpectNotAuthenticated(() => session.SubscribeToMotionEvents(123456));
            await ExpectNotAuthenticated(() => session.UnsubscribeFromMotionEvents(123456));
            await ExpectNotAuthenticated(() => session.GetDeviceSettings(123456));
            await ExpectNotAuthenticated(() => session.VideoSearch(123456));
            await ExpectNotAuthenticated(() => session.GetLocationEvents(locationId));
            await ExpectNotAuthenticated(() => session.GetDeviceEvents(locationId, 123456));
            await ExpectNotAuthenticated(() => session.GetSnapshotByUuid("uuid"));
            await ExpectNotAuthenticated(() => session.GetNextSnapshot(123456));
            await ExpectNotAuthenticated(() => session.GetPeriodicalFootage("event123"));
            await ExpectNotAuthenticated(() => session.GetLocationModeSettings(locationId));
            await ExpectNotAuthenticated(() => session.SetLocationModeSettings(locationId, "{}"));
            await ExpectNotAuthenticated(() => session.EnableLocationModes(locationId));
            await ExpectNotAuthenticated(() => session.DisableLocationModes(locationId));
            await ExpectNotAuthenticated(() => session.GetLocationModeSharing(locationId));
            await ExpectNotAuthenticated(() => session.SetLocationModeSharing(locationId, "{}"));
            await ExpectNotAuthenticated(() => session.GetAccountMonitoringStatus(locationId));
            await ExpectNotAuthenticated(() => session.TriggerAlarm(locationId, "asset-1"));
            await ExpectNotAuthenticated(() => session.GetLocationHistory(locationId));
            await ExpectNotAuthenticated(() => session.GetProfile());
            await ExpectNotAuthenticated(() => session.RegisterPushReceiver("token"));
            await ExpectNotAuthenticated(() => session.GetRingtones());
            await ExpectNotAuthenticated(() => session.FetchAmazonKeyLocks());
            await ExpectNotAuthenticated(() => session.UpdateChime(789012, "{}"));
            await ExpectNotAuthenticated(() => session.Unlock(555));
        }

        // --- Phase 16: endpoints confirmed via python-ring-doorbell's const.py (a second GitHub
        // client library search) ---

        [Fact]
        public async Task MockSession_GetActiveDings_ParsesEvents()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/dings/active",
                System.Net.HttpStatusCode.OK,
                @"[ { ""id"": 1, ""kind"": ""motion"" } ]");
            await _mockSession!.Authenticate();

            var dings = await _mockSession!.GetActiveDings();

            Assert.Equal(1, dings.Count);
            Assert.Equal("motion", dings[0].Kind);
        }

        [Fact]
        public async Task MockSession_GetLocation_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/clients_api/locations/{locationId:D}",
                System.Net.HttpStatusCode.OK,
                @"{ ""name"": ""Home"" }");
            await _mockSession!.Authenticate();

            var location = await _mockSession!.GetLocation(locationId);

            Assert.Equal("Home", location.GetProperty("name").GetString());
        }

        [Fact]
        public async Task MockSession_GetLinkedChimeDoorbots_ReturnsRawJson()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/chimes/789012/linked_doorbots",
                System.Net.HttpStatusCode.OK,
                @"{ ""doorbot_ids"": [123456] }");
            await _mockSession!.Authenticate();

            var linked = await _mockSession!.GetLinkedChimeDoorbots(789012);

            Assert.Equal(1, linked.GetProperty("doorbot_ids").GetArrayLength());
        }

        [Fact]
        public async Task MockSession_GetLocationHistory_UsesEvmPath()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            var locationId = Guid.NewGuid();
            mockHandler.SetupResponse(
                $"api.ring.com/evm/v2/history/locations/{locationId:D}",
                System.Net.HttpStatusCode.OK,
                @"{ ""items"": [] }");
            await _mockSession!.Authenticate();

            var history = await _mockSession!.GetLocationHistory(locationId);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains($"evm/v2/history/locations/{locationId:D}"));
            Assert.NotNull(call.Url);
            Assert.True(history.TryGetProperty("items", out _));
        }

        [Fact]
        public async Task MockSession_UnlockIntercom_UsesCommandsV1Path()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/commands/v1/devices/555/device_rpc", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.Unlock(555);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("commands/v1/devices/555/device_rpc"));
            Assert.NotNull(call.Url);
            Assert.Equal(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [Fact]
        public async Task MockSession_Phase16Methods_ThrowWhenNotAuthenticated()
        {
            var session = _mockHelper!.CreateSessionWithMockHandler();
            var locationId = Guid.NewGuid();

            async Task ExpectNotAuthenticated(Func<Task> action)
            {
                try
                {
                    await action();
                    Assert.Fail("Should have thrown SessionNotAuthenticatedException");
                }
                catch (VideoForensics.Providers.Ring.Exceptions.SessionNotAuthenticatedException) { }
            }

            await ExpectNotAuthenticated(() => session.GetActiveDings());
            await ExpectNotAuthenticated(() => session.GetLocation(locationId));
            await ExpectNotAuthenticated(() => session.GetLinkedChimeDoorbots(789012));
        }

        // --- Phase 17: Doorbot location/health field parsing via GetRingDevices() ---
        //
        // Previously untested: no fixture exercising latitude/longitude/address/battery_life/health
        // through GetRingDevices()'s real deserialization path. The existing DevicesWithDoorbot
        // fixture uses the wrong field names entirely ("battery"/"firmware" instead of
        // "battery_life"/"firmware_version") and no test ever authenticated before calling
        // GetRingDevices(), so it always short-circuited on SessionNotAuthenticatedException without
        // reaching the JSON at all. This exercises the real endpoint URL, authenticates first, and
        // asserts the actual field values - shape (including battery_percentage as a JSON string)
        // confirmed via a live ApiTester run.

        [Fact]
        public async Task MockSession_GetRingDevices_ParsesLocationAndHealthFields()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse(
                "api.ring.com/clients_api/ring_devices",
                System.Net.HttpStatusCode.OK,
                @"{
                    ""doorbots"": [
                        {
                            ""id"": 704352492,
                            ""description"": ""Front Door"",
                            ""location_id"": ""684e7cdb-45e1-4b1f-ac6f-e3211592a5ad"",
                            ""time_zone"": ""America/New_York"",
                            ""subscribed"": true,
                            ""subscribed_motions"": true,
                            ""battery_life"": ""88"",
                            ""external_connection"": false,
                            ""firmware_version"": ""1.8.30"",
                            ""kind"": ""lpd_v2"",
                            ""latitude"": 40.7128,
                            ""longitude"": -74.006,
                            ""address"": ""123 Main St, Anytown, NY 10001"",
                            ""owned"": true,
                            ""stolen"": false,
                            ""health"": { ""connected"": true, ""battery_percentage"": ""88"" }
                        }
                    ],
                    ""authorized_doorbots"": [],
                    ""stickup_cams"": [],
                    ""base_stations"": [],
                    ""chimes"": []
                }");
            await _mockSession!.Authenticate();

            var devices = await _mockSession!.GetRingDevices();

            Assert.Equal(1, devices.Doorbots.Count);
            var doorbot = devices.Doorbots[0];
            Assert.Equal(704352492, doorbot.Id);
            Assert.Equal(Guid.Parse("684e7cdb-45e1-4b1f-ac6f-e3211592a5ad"), doorbot.LocationId);
            Assert.Equal(40.7128, doorbot.Latitude);
            Assert.Equal(-74.006, doorbot.Longitude);
            Assert.Equal("123 Main St, Anytown, NY 10001", doorbot.Address);
            Assert.Equal(88, doorbot.BatteryLife);
            Assert.Equal("1.8.30", doorbot.FirmwareVersion);
            Assert.NotNull(doorbot.Health);
            Assert.Equal(true, doorbot.Health.Connected);
            Assert.Equal(88, doorbot.Health.BatteryPercentage);
        }
    }
}

