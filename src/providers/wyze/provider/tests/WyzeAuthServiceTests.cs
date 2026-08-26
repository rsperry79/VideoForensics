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
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            var result = await service.AuthenticateAsync("user@example.com", "password");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("not yet implemented", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RefreshAuthAsync_Stub_ReturnsFalse()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            var result = await service.RefreshAuthAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_Stub_ReturnsFalse()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            var result = await service.IsAuthenticatedAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetAuthStatus_Stub_ReturnsNotAuthenticatedMessage()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            var status = service.GetAuthStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Contains("Not authenticated", status);
        }

        [Fact]
        public void Constructor_WithLogger_CreatesService()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;

            // Act
            var service = new WyzeAuthService(logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task AuthenticateAsync_WithAnyCredentials_ReturnsFailureResult()
        {
            // Arrange
            var logger = new Mock<ILogger>().Object;
            var service = new WyzeAuthService(logger);

            // Act
            var result = await service.AuthenticateAsync("test", "test123");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
        }
    }
}
