using System.IO;
using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WebView2FixedRuntimeLocatorTests
    {
        [Fact]
        public void Resolve_ReturnsFolder_WhenRuntimeExecutableExists()
        {
            var folder = @"C:\fixed";
            var expectedExe = Path.Combine(@"C:\fixed", "msedgewebview2.exe");

            var result = WebView2FixedRuntimeLocator.Resolve(folder, path => path == expectedExe);

            Assert.Equal(@"C:\fixed", result);
        }

        [Fact]
        public void Resolve_ReturnsNull_WhenRuntimeExecutableMissing()
        {
            var result = WebView2FixedRuntimeLocator.Resolve(@"C:\fixed", _ => false);

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_ReturnsNull_WhenFolderBlank()
        {
            // Blank folder must not reach the file check, even if every path "exists".
            var result = WebView2FixedRuntimeLocator.Resolve("   ", _ => true);

            Assert.Null(result);
        }

        [Fact]
        public void DefaultFolder_IsUnderCommonAppData()
        {
            var expectedSuffix = Path.Combine("VideoForensics", "WebView2FixedVersion");

            Assert.EndsWith(expectedSuffix, WebView2FixedRuntimeLocator.DefaultFolder);
        }
    }
}
