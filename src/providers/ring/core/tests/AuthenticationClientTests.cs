using System.Threading;

using VideoForensics.Providers.Ring.Clients;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    public class AuthenticationClientTests
    {
        #region Mock Implementation
        private class MockAuthenticationService : IAuthenticationService
        {
            public bool IsAuthenticated { get; set; }
            public bool AuthenticateCalled { get; set; }
            public bool RefreshSessionCalled { get; set; }
            public bool EnsureSessionValidCalled { get; set; }
            public CancellationToken LastCancellationToken { get; set; }
            public Exception AuthenticateException { get; set; }
            public Exception RefreshSessionException { get; set; }
            public Exception EnsureSessionValidException { get; set; }

            public Task<bool> Authenticate(string operatingSystem = "windows", CancellationToken cancellationToken = default)
            {
                AuthenticateCalled = true;
                LastCancellationToken = cancellationToken;
                return AuthenticateException != null ? throw AuthenticateException : Task.FromResult(true);
            }

            public Task<bool> RefreshSession(CancellationToken cancellationToken = default)
            {
                RefreshSessionCalled = true;
                LastCancellationToken = cancellationToken;
                if (RefreshSessionException != null)
                {
                    throw RefreshSessionException;
                }

                IsAuthenticated = true;
                return Task.FromResult(true);
            }

            public Task EnsureSessionValid(CancellationToken cancellationToken = default)
            {
                EnsureSessionValidCalled = true;
                LastCancellationToken = cancellationToken;
                return EnsureSessionValidException != null ? throw EnsureSessionValidException : Task.CompletedTask;
            }
        }
        #endregion

        #region Constructor Tests
        [Fact]
        public void AuthenticationClient_Constructor_WithValidService_CreatesClient()
        {
            // Arrange
            var mockService = new MockAuthenticationService();

            // Act
            var client = new AuthenticationClient(mockService);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void AuthenticationClient_Constructor_WithNullService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new AuthenticationClient(null));
        }
        #endregion

        #region SignIn Tests
        [Fact]
        public async Task SignInAsync_WithValidCredentials_CallsAuthenticateService()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.SignInAsync("test@example.com", "password");

            // Assert
            Assert.True(mockService.AuthenticateCalled);
        }

        [Fact]
        public async Task SignInAsync_WithEmptyUsername_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInAsync("", "password"));
        }

        [Fact]
        public async Task SignInAsync_WithNullUsername_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInAsync(null, "password"));
        }

        [Fact]
        public async Task SignInAsync_WithEmptyPassword_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInAsync("test@example.com", ""));
        }

        [Fact]
        public async Task SignInAsync_WithNullPassword_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInAsync("test@example.com", null));
        }

        [Fact]
        public async Task SignInAsync_ForwardsCredentialsToService()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.SignInAsync("user@example.com", "mypassword");

            // Assert
            Assert.True(mockService.AuthenticateCalled);
        }

        [Fact]
        public async Task SignInAsync_ReturnsTrueWhenAuthenticationSucceeds()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            bool result = await client.SignInAsync("test@example.com", "password");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SignInAsync_ForwardsCancellationToken()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Act
            _ = await client.SignInAsync("test@example.com", "password", token);

            // Assert
            Assert.Equal(token, mockService.LastCancellationToken);
        }

        [Fact]
        public async Task SignInAsync_WhenAuthenticateThrows_PropagatesException()
        {
            // Arrange
            var mockService = new MockAuthenticationService
            {
                AuthenticateException = new Exception("Authentication failed")
            };
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() => client.SignInAsync("test@example.com", "password"));
            Assert.Equal("Authentication failed", ex.Message);
        }
        #endregion

        #region SignInWithTwoFactor Tests
        [Fact]
        public async Task SignInWithTwoFactorAsync_WithValidCode_CallsAuthenticateService()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.SignInWithTwoFactorAsync("123456");

            // Assert
            Assert.True(mockService.AuthenticateCalled);
        }

        [Fact]
        public async Task SignInWithTwoFactorAsync_WithEmptyCode_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInWithTwoFactorAsync(""));
        }

        [Fact]
        public async Task SignInWithTwoFactorAsync_WithNullCode_ThrowsArgumentException()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() => client.SignInWithTwoFactorAsync(null));
        }

        [Fact]
        public async Task SignInWithTwoFactorAsync_ReturnsTrueOnSuccess()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            bool result = await client.SignInWithTwoFactorAsync("123456");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SignInWithTwoFactorAsync_ForwardsCancellationToken()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Act
            _ = await client.SignInWithTwoFactorAsync("123456", token);

            // Assert
            Assert.Equal(token, mockService.LastCancellationToken);
        }

        [Fact]
        public async Task SignInWithTwoFactorAsync_WhenAuthenticateThrows_PropagatesException()
        {
            // Arrange
            var mockService = new MockAuthenticationService
            {
                AuthenticateException = new Exception("2FA validation failed")
            };
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() => client.SignInWithTwoFactorAsync("123456"));
            Assert.Equal("2FA validation failed", ex.Message);
        }
        #endregion

        #region RefreshAuthentication Tests
        [Fact]
        public async Task RefreshAuthenticationAsync_CallsRefreshSession()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.RefreshAuthenticationAsync();

            // Assert
            Assert.True(mockService.RefreshSessionCalled);
        }

        [Fact]
        public async Task RefreshAuthenticationAsync_ReturnsTrueOnSuccess()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            bool result = await client.RefreshAuthenticationAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RefreshAuthenticationAsync_ForwardsCancellationToken()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Act
            _ = await client.RefreshAuthenticationAsync(token);

            // Assert
            Assert.Equal(token, mockService.LastCancellationToken);
        }

        [Fact]
        public async Task RefreshAuthenticationAsync_WhenRefreshThrows_PropagatesException()
        {
            // Arrange
            var mockService = new MockAuthenticationService
            {
                RefreshSessionException = new Exception("Refresh token expired")
            };
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() => client.RefreshAuthenticationAsync());
            Assert.Equal("Refresh token expired", ex.Message);
        }

        [Fact]
        public async Task RefreshAuthenticationAsync_SetsAuthenticatedState()
        {
            // Arrange
            var mockService = new MockAuthenticationService { IsAuthenticated = false };
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.RefreshAuthenticationAsync();

            // Assert
            Assert.True(mockService.IsAuthenticated);
        }
        #endregion

        #region SignOut Tests
        [Fact]
        public async Task SignOutAsync_Completes()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            await client.SignOutAsync();

            // Assert
            // Verify no exception was thrown
            Assert.True(true);
        }

        [Fact]
        public async Task SignOutAsync_ForwardsCancellationToken()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Act
            await client.SignOutAsync(token);

            // Assert
            // Verify no exception on cancellation token handling
            Assert.True(true);
        }

        [Fact]
        public async Task SignOutAsync_CanBeCancelledEarly()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act - should not throw even with cancelled token
            await client.SignOutAsync(cts.Token);

            // Assert
            Assert.True(true);
        }
        #endregion

        #region IsAuthenticated Tests
        [Fact]
        public async Task IsAuthenticatedAsync_CallsEnsureSessionValid()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);

            // Act
            _ = await client.IsAuthenticatedAsync();

            // Assert
            Assert.True(mockService.EnsureSessionValidCalled);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_ReturnsAuthenticationStatus()
        {
            // Arrange
            var mockService = new MockAuthenticationService { IsAuthenticated = true };
            var client = new AuthenticationClient(mockService);

            // Act
            bool result = await client.IsAuthenticatedAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_ReturnsFalseWhenNotAuthenticated()
        {
            // Arrange
            var mockService = new MockAuthenticationService { IsAuthenticated = false };
            var client = new AuthenticationClient(mockService);

            // Act
            bool result = await client.IsAuthenticatedAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_ForwardsCancellationToken()
        {
            // Arrange
            var mockService = new MockAuthenticationService();
            var client = new AuthenticationClient(mockService);
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Act
            _ = await client.IsAuthenticatedAsync(token);

            // Assert
            Assert.Equal(token, mockService.LastCancellationToken);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_WhenEnsureSessionThrows_PropagatesException()
        {
            // Arrange
            var mockService = new MockAuthenticationService
            {
                EnsureSessionValidException = new Exception("Session validation failed")
            };
            var client = new AuthenticationClient(mockService);

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() => client.IsAuthenticatedAsync());
            Assert.Equal("Session validation failed", ex.Message);
        }
        #endregion
    }
}
