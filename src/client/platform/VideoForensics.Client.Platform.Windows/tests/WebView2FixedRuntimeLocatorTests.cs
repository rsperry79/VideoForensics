using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WebView2FixedRuntimeLocatorTests
    {
        [Fact]
        public void Resolve_ReturnsFolder_WhenRuntimeExecutableExists()
        {
            string folder = @"C:\fixed";
            string expectedExe = Path.Combine(@"C:\fixed", "msedgewebview2.exe");

            string? result = WebView2FixedRuntimeLocator.Resolve(folder, path => path == expectedExe);

            Assert.Equal(@"C:\fixed", result);
        }

        [Fact]
        public void Resolve_ReturnsNull_WhenRuntimeExecutableMissing()
        {
            string? result = WebView2FixedRuntimeLocator.Resolve(@"C:\fixed", _ => false);

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_ReturnsNull_WhenFolderBlank()
        {
            // Blank folder must not reach the file check, even if every path "exists".
            string? result = WebView2FixedRuntimeLocator.Resolve("   ", _ => true);

            Assert.Null(result);
        }

        [Fact]
        public void DefaultFolder_IsUnderCommonAppData()
        {
            string expectedSuffix = Path.Combine("VideoForensics", "WebView2FixedVersion");

            Assert.EndsWith(expectedSuffix, WebView2FixedRuntimeLocator.DefaultFolder);
        }
    }
}
