using VideoForensics.Hosting;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class CloudflaredTunnelServiceTests
    {
        [Fact]
        public void ResolveExecutablePath_WithNoBundledBinaryOrPath_ReturnsBareCloudflaredName()
        {
            // Guards against the executable name resolver silently drifting from "cloudflared"
            // (e.g. a typo like "cloudflare") which would make IsInstalledAsync() always report
            // not-installed even when a bundled/PATH binary exists.
            var candidatePath = Path.Combine(AppContext.BaseDirectory, ExecutableFileName());
            Assert.False(File.Exists(candidatePath), "Test assumes no bundled cloudflared binary is present");

            var result = CloudflaredTunnelService.ResolveExecutablePath();

            Assert.Equal("cloudflared", result);
        }

        [Fact]
        public void ResolveExecutablePath_WithBundledBinaryNextToApp_ReturnsFullPathToBundledBinary()
        {
            // This is the exact mechanism that makes cloudflared "detected" without a PATH
            // dependency: a copy of the executable placed next to the app's own binary
            // (AppContext.BaseDirectory) must take precedence over a bare PATH lookup.
            var candidatePath = Path.Combine(AppContext.BaseDirectory, ExecutableFileName());

            try
            {
                File.WriteAllText(candidatePath, "dummy");

                var result = CloudflaredTunnelService.ResolveExecutablePath();

                Assert.Equal(candidatePath, result);
            }
            finally
            {
                if (File.Exists(candidatePath))
                {
                    File.Delete(candidatePath);
                }
            }
        }

        private static string ExecutableFileName() => OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared";
    }
}
