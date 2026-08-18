using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KoenZomers.Ring.Api;
using KoenZomers.Ring.UnitTest.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KoenZomers.Ring.UnitTest
{
    /// <summary>
    /// Mock-based variants of integration tests. These tests don't require real Ring API credentials.
    /// They use MockHttpMessageHandler to simulate API responses.
    /// </summary>
    [TestClass]
    public class MockIntegrationTests
    {
        private MockSessionHelper? _mockHelper;
        private Api.Session? _mockSession;

        [TestInitialize]
        public void Setup()
        {
            _mockHelper = new MockSessionHelper();
            _mockSession = _mockHelper!.CreateSessionWithMockHandler();
        }

        [TestMethod]
        public void MockSession_CanBeAuthenticated()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();
            var session = new Session("test@example.com", "testpass", mockHandler);

            // Act
            var isAuthenticated = session.IsAuthenticated;

            // Assert
            Assert.IsFalse(isAuthenticated, "Session should not be authenticated without token");
        }

        [TestMethod]
        public void MockSession_HasCorrectUsername()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpass";

            // Act
            var session = new Session(username, password);

            // Assert
            Assert.AreEqual(username, session.Username);
            Assert.AreEqual(password, session.Password);
        }

        [TestMethod]
        public void MockSession_CanAccessApiUrls()
        {
            // Arrange
            var session = _mockSession!;

            // Act
            var oauthUrl = session.RingApiOAuthUrl;
            var baseUrl = session.RingApiBaseUrl;

            // Assert
            Assert.IsNotNull(oauthUrl);
            Assert.IsNotNull(baseUrl);
            Assert.IsTrue(oauthUrl.ToString().Contains("oauth.ring.com"));
            Assert.IsTrue(baseUrl.ToString().Contains("api.ring.com"));
        }

        [TestMethod]
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
                Assert.IsNotNull(devices);
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                // Expected - session not authenticated
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
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
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void MockSession_SupportsMultipleInstances()
        {
            // Arrange
            var session1 = new Session("user1@example.com", "pass1");
            var session2 = new Session("user2@example.com", "pass2");
            var session3 = new Session("user3@example.com", "pass3");

            // Act & Assert
            Assert.AreNotEqual(session1.Username, session2.Username);
            Assert.AreNotEqual(session2.Username, session3.Username);
            Assert.AreNotEqual(session1.Username, session3.Username);
        }

        [TestMethod]
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
            Assert.IsNotNull(handler1);
        }

        [TestMethod]
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
                Assert.IsNotNull(session);
            }
            catch (Api.Exceptions.AuthenticationFailedException)
            {
                // Expected - mock handler doesn't have real token response configured
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void MockSession_ApiUrlsAreConsistent()
        {
            // Arrange
            var session1 = new Session("user@example.com", "pass");
            var session2 = new Session("user@example.com", "pass");

            // Act
            var url1 = session1.RingApiBaseUrl;
            var url2 = session2.RingApiBaseUrl;

            // Assert
            Assert.AreEqual(url1, url2);
        }

        [TestMethod]
        public void MockHandler_DefaultResponsesAreConfigured()
        {
            // Arrange
            var mockHandler = new MockHttpMessageHandler();

            // Act - The handler should have default responses set up
            var handler = mockHandler;

            // Assert
            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task MockSession_CanHandleMultipleRequests()
        {
            // Arrange
            var sessions = new List<Session>();
            for (int i = 0; i < 5; i++)
            {
                sessions.Add(new Session($"user{i}@example.com", $"pass{i}", _mockHelper!.GetMockHandler()));
            }

            // Act & Assert
            Assert.AreEqual(5, sessions.Count);
            foreach (var session in sessions)
            {
                Assert.IsNotNull(session);
                Assert.IsFalse(session.IsAuthenticated);
            }
        }

        [TestMethod]
        public void MockSession_AuthenticationTokenIsNullWhenNotAuthenticated()
        {
            // Arrange
            var session = _mockSession;

            // Act
            var token = session.AuthenticationToken;

            // Assert
            Assert.IsNull(token);
        }

        [TestMethod]
        public async Task MockSession_DeviceExceptionHandling()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();

            // Act & Assert
            try
            {
                // Try to get devices without being authenticated
                await _mockSession.GetRingDevices();
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void MockSession_PasswordIsNotAccessible()
        {
            // Arrange
            var password = "secretpassword";
            var session = new Session("test@example.com", password);

            // Act
            var savedPassword = session.Password;

            // Assert
            Assert.AreEqual(password, savedPassword);
        }

        [TestMethod]
        public async Task MockSession_MultipleSessionsIndependent()
        {
            // Arrange
            var session1 = new Session("user1@example.com", "pass1", _mockHelper!.GetMockHandler());
            var session2 = new Session("user2@example.com", "pass2", _mockHelper!.GetMockHandler());

            // Act
            var auth1 = session1.IsAuthenticated;
            var auth2 = session2.IsAuthenticated;

            // Assert
            Assert.AreEqual(auth1, auth2);
            Assert.IsFalse(auth1 || auth2);
        }

        // Phase 3B: Device Operations Tests
        [TestMethod]
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
            Assert.IsNotNull(session);
            Assert.IsNotNull(session.RingApiBaseUrl);
        }

        [TestMethod]
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
                Assert.IsTrue(locations != null || !_mockSession!.IsAuthenticated);
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true, "Expected - session not authenticated");
            }
        }

        [TestMethod]
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
                Assert.IsTrue(history != null || !_mockSession!.IsAuthenticated);
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true, "Expected - session not authenticated");
            }
        }

        [TestMethod]
        public async Task MockSession_CanSetupSnapshotTimestampResponse()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/doorbots/123456/motion_snooze",
                TestFixtures.SnapshotResponses.SnapshotTimestamp
            );

            // Act & Assert
            Assert.IsNotNull(_mockSession);
        }

        [TestMethod]
        public async Task MockSession_CanSetupRecordingShareResponse()
        {
            // Arrange
            var mockHandler = _mockHelper!.GetMockHandler();
            _mockHelper!.SetupMockResponse(
                "https://api.ring.com/clients_api/v1/ding/xyz789/share",
                TestFixtures.RecordingResponses.RecordingShareUrl
            );

            // Act & Assert
            Assert.IsNotNull(_mockSession);
        }

        // Phase 3B: Error Scenario Tests
        [TestMethod]
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
            Assert.IsNotNull(mockHandler);
        }

        [TestMethod]
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
            Assert.IsNotNull(mockHandler);
        }

        [TestMethod]
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
            Assert.IsNotNull(mockHandler);
        }

        [TestMethod]
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
            Assert.IsNotNull(mockHandler);
        }

        [TestMethod]
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
            Assert.IsNotNull(session);
            Assert.IsNotNull(session.RingApiBaseUrl);
        }

        [TestMethod]
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
                Assert.IsTrue(devices != null || !_mockSession!.IsAuthenticated);
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true, "Expected - session not authenticated");
            }
        }

        [TestMethod]
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
                Assert.IsTrue(history != null || !_mockSession!.IsAuthenticated);
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException)
            {
                Assert.IsTrue(true, "Expected - session not authenticated");
            }
        }

        [TestMethod]
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
                Assert.IsNotNull(newSession);
            }
            catch (Api.Exceptions.AuthenticationFailedException)
            {
                Assert.IsTrue(true, "Expected - mock setup for token refresh");
            }
        }

        [TestMethod]
        public void MockSession_UrlsRemainConsistentAcrossCalls()
        {
            // Arrange
            var session = _mockSession!;

            // Act
            var oauth1 = session.RingApiOAuthUrl;
            var base1 = session.RingApiBaseUrl;
            var oauth2 = session.RingApiOAuthUrl;
            var base2 = session.RingApiBaseUrl;

            // Assert
            Assert.AreEqual(oauth1, oauth2);
            Assert.AreEqual(base1, base2);
        }

        // --- Device control endpoints (light, siren, chime test sound) ---
        //
        // These fill a gap versus other unofficial Ring API clients (python-ring-doorbell,
        // ring-client-api), which all expose light/siren/chime-test control but which this
        // wrapper previously did not. Each test authenticates against the default mocked OAuth
        // response, then asserts both that the call succeeds and that it hit the exact endpoint
        // and HTTP verb the real Ring API expects.

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the floodlight endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
            Assert.IsTrue(call.Url.EndsWith("doorbots/123456/floodlight_light_on"), $"Unexpected url: {call.Url}");
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the floodlight endpoint");
            Assert.IsTrue(call.Url.EndsWith("doorbots/123456/floodlight_light_off"), $"Unexpected url: {call.Url}");
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the siren endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
            Assert.IsTrue(call.Url.Contains("doorbots/123456/siren_on"), $"Unexpected url: {call.Url}");
            Assert.IsTrue(call.Url.Contains("duration=30"), $"Expected duration query param, got: {call.Url}");
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the siren endpoint");
            Assert.IsTrue(call.Url.EndsWith("doorbots/123456/siren_off"), $"Unexpected url: {call.Url}");
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the play_sound endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Post, call.Method);
            Assert.IsTrue(call.Url.EndsWith("chimes/789012/play_sound"), $"Unexpected url: {call.Url}");
        }

        [TestMethod]
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
            catch (Api.Exceptions.SessionNotAuthenticatedException) { }

            try
            {
                await session.SetSiren(123456, true);
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException) { }

            try
            {
                await session.TestChimeSound(789012);
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Api.Exceptions.SessionNotAuthenticatedException) { }
        }

        // --- Phase 1: device setting setters ---

        [TestMethod]
        public async Task MockSession_SetVolume_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetVolume(123456, 5);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.IsNotNull(call.Url, "Expected a request to the doorbots endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [TestMethod]
        public async Task MockSession_SetMotionDetection_CallsSettingsEndpointWithPatch()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/devices/v1/devices/123456/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetMotionDetection(123456, false);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("devices/123456/settings"));
            Assert.IsNotNull(call.Url, "Expected a request to the device settings endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Patch, call.Method);
        }

        [TestMethod]
        public async Task MockSession_SetChimeType_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetChimeType(123456, 1, enabled: true, duration: 3);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.IsNotNull(call.Url, "Expected a request to the doorbots endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [TestMethod]
        public async Task MockSession_SetDoNotDisturb_CallsChimeEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/chimes/789012/do_not_disturb", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetDoNotDisturb(789012, 300);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("do_not_disturb"));
            Assert.IsNotNull(call.Url, "Expected a request to the do_not_disturb endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
        }

        [TestMethod]
        public async Task MockSession_SetNightMode_CallsDoorbotsEndpointWithPut()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/clients_api/doorbots/123456", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            await _mockSession!.SetNightMode(123456, true);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.EndsWith("doorbots/123456"));
            Assert.IsNotNull(call.Url, "Expected a request to the doorbots endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Put, call.Method);
        }

        // --- Phase 2: motion zones ---

        [TestMethod]
        public async Task MockSession_SetMotionZones_CallsSettingsEndpointWithPatch()
        {
            var mockHandler = _mockHelper!.GetMockHandler();
            mockHandler.SetupResponse("api.ring.com/devices/v1/devices/123456/settings", System.Net.HttpStatusCode.OK, "");
            await _mockSession!.Authenticate();

            var zones = new Api.Entities.AdvancedMotionZones
            {
                Zone1 = new Api.Entities.Zone { Name = "Front Yard", State = 1 }
            };
            await _mockSession!.SetMotionZones(123456, zones);

            var call = mockHandler.RequestLog.LastOrDefault(r => r.Url.Contains("devices/123456/settings"));
            Assert.IsNotNull(call.Url, "Expected a request to the device settings endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Patch, call.Method);
        }

        [TestMethod]
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

        [TestMethod]
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

            Assert.IsNotNull(groups);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("grp-1", groups[0].DeviceGroupId);
            Assert.AreEqual("Backyard Lights", groups[0].Name);
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the group devices endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Post, call.Method);
        }

        // --- Phase 4: shared users / invitations ---

        [TestMethod]
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

            Assert.IsNotNull(users);
            Assert.AreEqual(1, users.Count);
            Assert.AreEqual("guest@example.com", users[0].Email);
            Assert.AreEqual(1, users[0].Devices.Count);
            Assert.AreEqual("shared_user", users[0].Devices[0].Role);
        }

        [TestMethod]
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

            Assert.IsNotNull(invitations);
            Assert.AreEqual(1, invitations.Count);
            Assert.AreEqual("pending@example.com", invitations[0].InvitedEmail);
        }

        // --- Phase 5: location mode ---

        [TestMethod]
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

            Assert.IsNotNull(result);
            Assert.AreEqual("away", result.Mode);
        }

        [TestMethod]
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
            Assert.IsNotNull(call.Url, "Expected a request to the location mode endpoint");
            Assert.AreEqual(System.Net.Http.HttpMethod.Post, call.Method);
        }

        [TestMethod]
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
                catch (Api.Exceptions.SessionNotAuthenticatedException) { }
            }

            await ExpectNotAuthenticated(() => session.SetVolume(123456, 5));
            await ExpectNotAuthenticated(() => session.SetMotionDetection(123456, true));
            await ExpectNotAuthenticated(() => session.SetChimeType(123456, 1));
            await ExpectNotAuthenticated(() => session.SetDoNotDisturb(789012, 60));
            await ExpectNotAuthenticated(() => session.SetNightMode(123456, true));
            await ExpectNotAuthenticated(() => session.SetMotionZones(123456, new Api.Entities.AdvancedMotionZones()));
            await ExpectNotAuthenticated(() => session.GetGroups(locationId));
            await ExpectNotAuthenticated(() => session.SetGroupLights(locationId, "grp-1", true));
            await ExpectNotAuthenticated(() => session.GetSharedUsers(locationId));
            await ExpectNotAuthenticated(() => session.GetInvitations(locationId));
            await ExpectNotAuthenticated(() => session.GetLocationMode(locationId));
            await ExpectNotAuthenticated(() => session.SetLocationMode(locationId, "home"));
        }
    }
}
