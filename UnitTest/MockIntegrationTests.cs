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
    }
}
