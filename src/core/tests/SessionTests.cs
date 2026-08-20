using Ring.Api;

using Ring.Api.Tests.Mocks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ring.Api.Tests
{
    [TestClass]
    public class SessionTests
    {
        [TestMethod]
        public void Session_Constructor_WithCredentials_CreatesSession()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpassword";

            // Act
            var session = new Session(username, password);

            // Assert
            Assert.IsNotNull(session);
            Assert.AreEqual(username, session.Username);
            Assert.AreEqual(password, session.Password);
            Assert.IsFalse(session.IsAuthenticated);
        }

        [TestMethod]
        public void Session_Constructor_WithMockHandler_CreatesSession()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpassword";
            var mockHandler = new MockHttpMessageHandler();

            // Act
            var session = new Session(username, password, mockHandler);

            // Assert
            Assert.IsNotNull(session);
            Assert.AreEqual(username, session.Username);
            Assert.AreEqual(password, session.Password);
        }

        [TestMethod]
        public void Session_IsAuthenticated_WithoutToken_ReturnsFalse()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var isAuthenticated = session.IsAuthenticated;

            // Assert
            Assert.IsFalse(isAuthenticated);
        }

        [TestMethod]
        public void Session_AuthenticationToken_WithoutAuth_ReturnsNull()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var token = session.AuthenticationToken;

            // Assert
            Assert.IsNull(token);
        }

        [TestMethod]
        public void Session_WithMockHandler_CanBeInstantiated()
        {
            // Arrange
            var helper = new MockSessionHelper();

            // Act
            var mockSession = helper.CreateSessionWithMockHandler();

            // Assert
            Assert.IsNotNull(mockSession);
            Assert.IsFalse(mockSession.IsAuthenticated);
        }

        [TestMethod]
        public void Session_OAuthUrl_IsCorrect()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var oauthUrl = session.OAuthUrl;

            // Assert
            Assert.IsNotNull(oauthUrl);
            Assert.AreEqual("https://oauth.ring.com/oauth/token", oauthUrl.ToString());
        }

        [TestMethod]
        public void Session_BaseUrl_IsCorrect()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var baseUrl = session.BaseUrl;

            // Assert
            Assert.IsNotNull(baseUrl);
            Assert.IsTrue(baseUrl.ToString().Contains("https://api.ring.com"));
        }

        [TestMethod]
        public void Session_CanBeCreatedMultipleTimes()
        {
            // Arrange & Act
            var session1 = new Session("user1@example.com", "pass1");
            var session2 = new Session("user2@example.com", "pass2");
            var session3 = new Session("user3@example.com", "pass3");

            // Assert
            Assert.IsNotNull(session1);
            Assert.IsNotNull(session2);
            Assert.IsNotNull(session3);
            Assert.AreNotEqual(session1.Username, session2.Username);
            Assert.AreNotEqual(session2.Username, session3.Username);
        }
    }
}
