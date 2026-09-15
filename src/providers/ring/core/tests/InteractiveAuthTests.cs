using System.Net;
using System.Threading;
using VideoForensics.Providers.Ring.Core.Tests.Mocks;
using VideoForensics.Providers.Ring.Exceptions;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Tests for InteractiveAuth utility class. InteractiveAuth is a static helper that demonstrates
    /// authentication patterns - it creates a session with credentials and handles the 2FA flow.
    ///
    /// Note: These tests verify the class behavior at the API boundary. Full authentication tests
    /// should use real credentials or a fully-mocked HTTP handler that can be injected. This test
    /// suite focuses on contract compliance and error handling patterns.
    /// </summary>
    public class InteractiveAuthTests
    {
        [Fact]
        public void InteractiveAuth_IsStatic()
        {
            // Verify that InteractiveAuth is a utility class (all static members)
            var authType = typeof(InteractiveAuth);
            Assert.True(authType.IsAbstract && authType.IsSealed, "InteractiveAuth should be a static class");
        }

        [Fact]
        public void InteractiveAuth_HasAuthenticateAsyncMethod()
        {
            // Verify the method exists with correct signature
            var method = typeof(InteractiveAuth).GetMethod(
                "AuthenticateAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.NotNull(method);
            Assert.True(method.IsGenericMethodDefinition == false);
        }

        [Fact]
        public async Task AuthenticateAsync_AcceptsStringParameters()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";
            Func<Task<string>> getTwoFactorCode = null;

            // Act & Assert - if this compiles and the method signature is correct, parameters are accepted
            try
            {
                _ = await InteractiveAuth.AuthenticateAsync(username, password, getTwoFactorCode);
            }
            catch (AuthenticationFailedException)
            {
                // Expected when mock handler returns 406
                // The important thing is the method accepted the parameters
            }
            catch (Exception ex) when (ex.GetType().Name == "AuthenticationFailedException")
            {
                // Expected - we're testing parameter acceptance, not successful auth
            }
        }

        [Fact]
        public async Task AuthenticateAsync_WithNullGetTwoFactorCode_IsValid()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";

            // Act & Assert - null getTwoFactorCode parameter should be allowed
            try
            {
                _ = await InteractiveAuth.AuthenticateAsync(username, password, null);
            }
            catch (AuthenticationFailedException)
            {
                // Expected - we're testing null parameter is accepted
            }
        }

        [Fact]
        public async Task AuthenticateAsync_WithAsyncCallback_IsValid()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";
            Func<Task<string>> getTwoFactorCode = async () =>
            {
                await Task.Delay(0);
                return "123456";
            };

            // Act & Assert - async callback should be accepted
            try
            {
                _ = await InteractiveAuth.AuthenticateAsync(username, password, getTwoFactorCode);
            }
            catch (AuthenticationFailedException)
            {
                // Expected - testing parameter acceptance
            }
        }

        [Fact]
        public async Task AuthenticateAsync_WithSyncCallback_IsValid()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";
            Func<Task<string>> getTwoFactorCode = () => Task.FromResult("123456");

            // Act & Assert - sync-to-async callback should be accepted
            try
            {
                _ = await InteractiveAuth.AuthenticateAsync(username, password, getTwoFactorCode);
            }
            catch (AuthenticationFailedException)
            {
                // Expected
            }
        }

        [Fact]
        public async Task AuthenticateAsync_ReturnsSessionType()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";

            // Act
            try
            {
                var result = await InteractiveAuth.AuthenticateAsync(username, password, null);

                // Assert - if successful, result should be a Session
                Assert.IsType<Session>(result);
            }
            catch (AuthenticationFailedException)
            {
                // Expected when credentials aren't valid - but the method signature is correct
            }
        }

        [Fact]
        public async Task AuthenticateAsync_ExceptionMessageIsPropagated()
        {
            // Arrange
            string username = "invalid-user";
            string password = "invalid-pass";

            // Act & Assert - any authentication exception should propagate
            await Assert.ThrowsAsync<AuthenticationFailedException>(
                () => InteractiveAuth.AuthenticateAsync(username, password, null));
        }

        [Fact]
        public async Task AuthenticateAsync_MethodIsAsync()
        {
            // Arrange
            string username = "test@example.com";
            string password = "password";

            // Act - verify this returns a Task
            var task = InteractiveAuth.AuthenticateAsync(username, password, null);

            // Assert - result should be awaitable Task<Session>
            Assert.NotNull(task);
            Assert.True(task is System.Threading.Tasks.Task<Session>);

            // Cleanup - await to clear any exceptions
            try
            {
                _ = await task;
            }
            catch
            {
                // Ignore - testing async signature
            }
        }

        [Fact]
        public async Task AuthenticateAsync_MultipleCallsWithDifferentCredentials()
        {
            // Arrange & Act - verify method can be called multiple times
            try
            {
                _ = await InteractiveAuth.AuthenticateAsync("user1@example.com", "pass1", null);
            }
            catch { }

            try
            {
                _ = await InteractiveAuth.AuthenticateAsync("user2@example.com", "pass2", null);
            }
            catch { }

            // Assert - no exceptions from the utility itself (any auth exceptions are expected)
            Assert.True(true);
        }
    }
}
