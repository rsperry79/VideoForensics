using Xunit;

namespace VideoForensics.Core.Providers.Core.Tests
{
    public class FfmpegPathResolverTests
    {
        [Fact]
        public void Resolve_WithConfiguredPath_ReturnsConfiguredPathUnchanged()
        {
            // Arrange
            string configuredPath = "/custom/path/to/ffmpeg";
            string executableBaseName = "ffmpeg";

            // Act
            string result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName);

            // Assert
            Assert.Equal(configuredPath, result);
        }

        [Fact]
        public void Resolve_WithNullConfiguredPathAndNoBundledBinary_ReturnsBareExecutableName()
        {
            // Arrange
            string executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            string result = FfmpegPathResolver.Resolve(null, executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithEmptyConfiguredPath_ReturnsBareExecutableName()
        {
            // Arrange
            string executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            string result = FfmpegPathResolver.Resolve(string.Empty, executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithWhitespaceConfiguredPath_ReturnsBareExecutableName()
        {
            // Arrange
            string executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            // Act
            string result = FfmpegPathResolver.Resolve("   ", executableBaseName);

            // Assert
            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithBundledBinaryPresent_ReturnsFullPathToBundledBinary()
        {
            // Arrange
            string executableBaseName = "test-ffmpeg-resolver-temp";
            string executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;
            string tempFilePath = Path.Combine(AppContext.BaseDirectory, executableName);

            try
            {
                // Create the temporary bundled binary
                File.WriteAllText(tempFilePath, "dummy");
                Assert.True(File.Exists(tempFilePath), "Temp file should exist before test");

                // Act
                string result = FfmpegPathResolver.Resolve(null, executableBaseName);

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
            string executableBaseName = "test-bindir-explicit-wins";
            string dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            string exeName = OperatingSystem.IsWindows() ? executableBaseName + ".exe" : executableBaseName;
            File.WriteAllText(Path.Combine(dir, exeName), "dummy");
            try
            {
                string configuredPath = "/explicitly/configured/ffmpeg";

                string result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName, dir);

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
            string executableBaseName = "test-bindir-exe-present";
            string dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            string exeName = OperatingSystem.IsWindows() ? executableBaseName + ".exe" : executableBaseName;
            string exePath = Path.Combine(dir, exeName);
            File.WriteAllText(exePath, "dummy");
            try
            {
                string result = FfmpegPathResolver.Resolve(null, executableBaseName, dir);

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
            string executableBaseName = "test-bindir-exe-missing";
            string dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            try
            {
                string result = FfmpegPathResolver.Resolve(null, executableBaseName, dir);

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
            string executableBaseName = "nonexistent-ffmpeg-xyz-test-binary";

            string result = FfmpegPathResolver.Resolve(null, executableBaseName);

            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_WithBundledBinaryPresentAndConfiguredPath_ReturnsConfiguredPath()
        {
            // Arrange
            string configuredPath = "/explicitly/configured/ffmpeg";
            string executableBaseName = "test-ffmpeg-resolver-temp2";
            string executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;
            string tempFilePath = Path.Combine(AppContext.BaseDirectory, executableName);

            try
            {
                // Create the temporary bundled binary
                File.WriteAllText(tempFilePath, "dummy");
                Assert.True(File.Exists(tempFilePath), "Temp file should exist before test");

                // Act
                string result = FfmpegPathResolver.Resolve(configuredPath, executableBaseName);

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

        [Fact]
        public void Resolve_NoConfigAndDiscoverFinds_ReturnsDiscoveredPath()
        {
            string executableBaseName = "test-discover-finds-xyz";
            string expected = "C:/found/" + executableBaseName;

            string result = FfmpegPathResolver.Resolve(null, executableBaseName, null, name => "C:/found/" + name);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Resolve_NoConfigAndDiscoverReturnsNull_ReturnsBareName()
        {
            string executableBaseName = "test-discover-null-xyz";

            string result = FfmpegPathResolver.Resolve(null, executableBaseName, null, _ => null);

            Assert.Equal(executableBaseName, result);
        }

        [Fact]
        public void Resolve_BinDirectoryHasExe_DoesNotCallDiscover()
        {
            string executableBaseName = "test-discover-bindir-wins";
            string dir = Path.Combine(Path.GetTempPath(), "vf-resolver-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(dir);
            string exeName = OperatingSystem.IsWindows() ? executableBaseName + ".exe" : executableBaseName;
            string exePath = Path.Combine(dir, exeName);
            File.WriteAllText(exePath, "dummy");
            try
            {
                string result = FfmpegPathResolver.Resolve(null, executableBaseName, dir, _ => throw new InvalidOperationException());

                Assert.Equal(exePath, result);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Resolve_ConfiguredPath_DoesNotCallDiscover()
        {
            string configuredPath = "/explicitly/configured/ffmpeg";

            string result = FfmpegPathResolver.Resolve(configuredPath, "test-discover-config-wins", null, _ => throw new InvalidOperationException());

            Assert.Equal(configuredPath, result);
        }
    }
}
