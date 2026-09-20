using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Wyze.Services;

using Xunit;

namespace VideoForensics.Providers.Wyze.Tests
{
    /// <summary>
    /// Tests for WyzeAuthService stub implementation.
    /// Verifies that unimplemented methods return failure results.
    /// </summary>
    public class WyzeAuthServiceTests
    {
        [Fact]
        public async Task AuthenticateAsync_Stub_ReturnsFailureResult()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            AuthResult result = await service.AuthenticateAsync("user@example.com", "password");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("not yet implemented", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RefreshAuthAsync_Stub_ReturnsFalse()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            bool result = await service.RefreshAuthAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_Stub_ReturnsFalse()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            bool result = await service.IsAuthenticatedAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetAuthStatus_Stub_ReturnsNotAuthenticatedMessage()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            string status = service.GetAuthStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Contains("Not authenticated", status);
        }

        [Fact]
        public void Constructor_WithLogger_CreatesService()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;

            // Act
            var service = new WyzeAuthService(logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task AuthenticateAsync_WithAnyCredentials_ReturnsFailureResult()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            AuthResult result = await service.AuthenticateAsync("test", "test123");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task AuthenticateAsync_DoesNotLogRawUsername()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var service = new WyzeAuthService(mockLogger.Object);
            const string testUsername = "user@example.com";

            // Act
            await service.AuthenticateAsync(testUsername, "password");

            // Assert - verify LogInformation was called, but NOT with the raw username
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains(testUsername)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_DoesNotLogRawUsername()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var service = new WyzeAuthService(mockLogger.Object);
            const string testUsername = "user@example.com";
            var twoFactorProvider = () => Task.FromResult("123456");

            // Act
            await service.AuthenticateWithTwoFactorAsync(testUsername, "password", twoFactorProvider);

            // Assert - verify LogInformation was called, but NOT with the raw username
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains(testUsername)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}
