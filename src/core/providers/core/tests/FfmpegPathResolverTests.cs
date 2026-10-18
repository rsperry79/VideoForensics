using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
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
        public void Resolve_ExplicitConfiguredPathAndBinDirectoryWithExe_ReturnsConfiguredPath()
        {
            var executableBaseName = "test-bindir-explicit-wins";
            var dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            var exeName = OperatingSystem.IsWindows() ? executableBaseName + ".exe" : executableBaseName;
            File.WriteAllText(Path.Combine(dir, exeName), "dummy");
            try
            {
                var configuredPath = "/explicitly/configured/ffmpeg";

                var result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName, dir);

                Assert.Equal(configuredPath, result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Resolve_BinDirectoryContainsExe_ReturnsFullPathInBinDirectory()
        {
            var executableBaseName = "test-bindir-exe-present";
            var dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            var exeName = OperatingSystem.IsWindows() ? executableBaseName + ".exe" : executableBaseName;
            var exePath = Path.Combine(dir, exeName);
            File.WriteAllText(exePath, "dummy");
            try
            {
                var result = FfmpegPathResolver.Resolve(null, executableBaseName, dir);

                Assert.Equal(exePath, result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Resolve_BinDirectoryWithoutExe_FallsThroughToBareName()
        {
            var executableBaseName = "test-bindir-exe-missing";
            var dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            try
            {
                var result = FfmpegPathResolver.Resolve(null, executableBaseName, dir);

                Assert.Equal(executableBaseName, result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Resolve_TwoArgCall_BehavesAsBeforeWithNoBinDirectory()
        {
            var executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            var result = FfmpegPathResolver.Resolve(null, executableBaseName);

            Assert.Equal(executableBaseName, result);
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
