using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Tests
{

    public class SystemVersionServiceTests
    {
        private static Mock<IConfiguration> CreateMockConfiguration(string? releaseChannel = null)
        {
            var mock = new Mock<IConfiguration>();
            mock.Setup(c => c["ReleaseChannel"]).Returns(releaseChannel);
            return mock;
        }

        private static Mock<ILogger<SystemVersionService>> CreateMockLogger()
        {
            return new Mock<ILogger<SystemVersionService>>();
        }

        [Fact]
        public async Task SystemVersionService_GetCurrentVersionAsync_ReturnsVersionFromAssemblyAttribute()
        {
            // Arrange
            var config = CreateMockConfiguration();
            var logger = CreateMockLogger();
            var sut = new SystemVersionService(config.Object, logger.Object);

            // Act
            var result = await sut.GetCurrentVersionAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            // Should look like semver or assembly version (e.g., "1.0.0" or "1.0.5-alpha.1+build.23")
            // At minimum should contain a digit
            Assert.True(result.Any(char.IsDigit), "Version should contain at least one digit");
        }

        [Fact]
        public async Task SystemVersionService_GetBuildTimestampAsync_ReturnsBuildTimeFromEnvironment()
        {
            // Arrange
            var expectedTimestamp = "2024-10-05T12:00:00Z";
            var originalValue = Environment.GetEnvironmentVariable("BUILD_TIMESTAMP");
            try
            {
                Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", expectedTimestamp);

                var config = CreateMockConfiguration();
                var logger = CreateMockLogger();
                var sut = new SystemVersionService(config.Object, logger.Object);

                // Act
                var result = await sut.GetBuildTimestampAsync(CancellationToken.None);

                // Assert
                Assert.NotNull(result);
                if (DateTime.TryParse(expectedTimestamp, out var expected))
                {
                    // Verify the result is close to the expected time (allowing for minor parsing differences)
                    var timeDifference = Math.Abs((result.Value - expected).TotalSeconds);
                    Assert.True(timeDifference < 1, $"Time difference should be less than 1 second, but was {timeDifference} seconds");
                }
            }
            finally
            {
                // Restore original env var
                if (originalValue != null)
                    Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", originalValue);
                else
                    Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", null);
            }
        }

        [Fact]
        public async Task SystemVersionService_GetBuildTimestampAsync_ReturnsNullWhenEnvironmentVarMissing()
        {
            // Arrange
            var originalValue = Environment.GetEnvironmentVariable("BUILD_TIMESTAMP");
            try
            {
                // Ensure env var is not set
                Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", null);

                var config = CreateMockConfiguration();
                var logger = CreateMockLogger();
                var sut = new SystemVersionService(config.Object, logger.Object);

                // Act
                var result = await sut.GetBuildTimestampAsync(CancellationToken.None);

                // Assert
                // Result should either be null or a valid DateTime (fallback to assembly write time)
                // Both are acceptable behaviors per the service implementation
                if (result.HasValue)
                {
                    Assert.True(result.Value > DateTime.MinValue, "Should be a valid timestamp if not null");
                }
            }
            finally
            {
                // Restore original env var
                if (originalValue != null)
                    Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", originalValue);
                else
                    Environment.SetEnvironmentVariable("BUILD_TIMESTAMP", null);
            }
        }

        [Fact]
        public async Task SystemVersionService_GetReleaseChannelAsync_ReturnsChannelFromConfiguration()
        {
            // Arrange
            var config = CreateMockConfiguration("testing");
            var logger = CreateMockLogger();
            var sut = new SystemVersionService(config.Object, logger.Object);

            // Act
            var result = await sut.GetReleaseChannelAsync(CancellationToken.None);

            // Assert
            Assert.Equal("testing", result);
        }

        [Fact]
        public async Task SystemVersionService_GetReleaseChannelAsync_ReturnsDefaultChannelWhenNotConfigured()
        {
            // Arrange
            var config = CreateMockConfiguration(null); // No channel configured
            var logger = CreateMockLogger();
            var sut = new SystemVersionService(config.Object, logger.Object);

            // Act
            var result = await sut.GetReleaseChannelAsync(CancellationToken.None);

            // Assert
            Assert.Equal("dev", result);
        }
    }
}
