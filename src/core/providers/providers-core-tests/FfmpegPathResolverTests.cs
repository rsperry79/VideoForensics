using Xunit;

namespace VideoForensics.Providers.Core.Tests
{
    public class FfmpegPathResolverTests
    {
        [Fact]
        public void Resolve_WithConfiguredPath_ReturnsConfiguredPathUnchanged()
        {
            // Arrange
            var configuredPath = "/custom/path/to/ffmpeg";
            var executableBaseName = "ffmpeg";

            // Act
            var result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName);

            // Assert
            Assert.Equal(configuredPath, result);
        }

        [Fact]
        public void Resolve_WithNullConfiguredPathAndNoBundledBinary_ReturnsBareExecutableName()
        {
            // Arrange
            var executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            var result = FfmpegPathResolver.Resolve(null, executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithEmptyConfiguredPath_ReturnsBareExecutableName()
        {
            // Arrange
            var executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            var result = FfmpegPathResolver.Resolve("", executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithWhitespaceConfiguredPath_ReturnsBareExecutableName()
        {
            // Arrange
            var executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            var result = FfmpegPathResolver.Resolve("   ", executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithBundledBinaryPresent_ReturnsFullPathToBundledBinary()
        {
            // Arrange
            var executableBaseName = "test-ffmpeg-resolver-temp";
            var executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;
            var tempFilePath = Path.Combine(AppContext.BaseDirectory, executableName);

            try
            {
                // Create the temporary bundled binary
                File.WriteAllText(tempFilePath, "dummy");
                Assert.True(File.Exists(tempFilePath), "Temp file should exist before test");

                // Act
                var result = FfmpegPathResolver.Resolve(null, executableBaseName);

                // Assert
                Assert.Equal(tempFilePath, result);
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public void Resolve_WithBundledBinaryPresentAndConfiguredPath_ReturnsConfiguredPath()
        {
            // Arrange
            var configuredPath = "/explicitly/configured/ffmpeg";
            var executableBaseName = "test-ffmpeg-resolver-temp2";
            var executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;
            var tempFilePath = Path.Combine(AppContext.BaseDirectory, executableName);

            try
            {
                // Create the temporary bundled binary
                File.WriteAllText(tempFilePath, "dummy");
                Assert.True(File.Exists(tempFilePath), "Temp file should exist before test");

                // Act
                var result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName);

                // Assert
                // Configured path should take precedence over bundled binary
                Assert.Equal(configuredPath, result);
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
    }
}
