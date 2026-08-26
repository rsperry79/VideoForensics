using VideoForensics.Providers.Ring;

using VideoForensics.Providers.Ring.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Tests
{
    public class SessionTests
    {
        [Fact]
        public void Session_Constructor_WithCredentials_CreatesSession()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpassword";

            // Act
            var session = new Session(username, password);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(username, session.Username);
            Assert.Equal(password, session.Password);
            Assert.False(session.IsAuthenticated);
        }

        [Fact]
        public void Session_Constructor_WithMockHandler_CreatesSession()
        {
            // Arrange
            var username = "test@example.com";
            var password = "testpassword";
            var mockHandler = new MockHttpMessageHandler();

            // Act
            var session = new Session(username, password, mockHandler);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(username, session.Username);
            Assert.Equal(password, session.Password);
        }

        [Fact]
        public void Session_IsAuthenticated_WithoutToken_ReturnsFalse()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var isAuthenticated = session.IsAuthenticated;

            // Assert
            Assert.False(isAuthenticated);
        }

        [Fact]
        public void Session_AuthenticationToken_WithoutAuth_ReturnsNull()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var token = session.AuthenticationToken;

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public void Session_WithMockHandler_CanBeInstantiated()
        {
            // Arrange
            var helper = new MockSessionHelper();

            // Act
            var mockSession = helper.CreateSessionWithMockHandler();

            // Assert
            Assert.NotNull(mockSession);
            Assert.False(mockSession.IsAuthenticated);
        }

        [Fact]
        public void Session_OAuthUrl_IsCorrect()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var oauthUrl = session.OAuthUrl;

            // Assert
            Assert.NotNull(oauthUrl);
            Assert.Equal("https://oauth.ring.com/oauth/token", oauthUrl.ToString());
        }

        [Fact]
        public void Session_BaseUrl_IsCorrect()
        {
            // Arrange
            var session = new Session("test@example.com", "password");

            // Act
            var baseUrl = session.BaseUrl;

            // Assert
            Assert.NotNull(baseUrl);
            Assert.True(baseUrl.ToString().Contains("https://api.ring.com"));
        }

        [Fact]
        public void Session_CanBeCreatedMultipleTimes()
        {
            // Arrange & Act
            var session1 = new Session("user1@example.com", "pass1");
            var session2 = new Session("user2@example.com", "pass2");
            var session3 = new Session("user3@example.com", "pass3");

            // Assert
            Assert.NotNull(session1);
            Assert.NotNull(session2);
            Assert.NotNull(session3);
            Assert.NotEqual(session1.Username, session2.Username);
            Assert.NotEqual(session2.Username, session3.Username);
        }
    }
}
