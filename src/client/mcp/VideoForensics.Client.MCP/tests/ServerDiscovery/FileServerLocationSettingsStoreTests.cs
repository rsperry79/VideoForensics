using VideoForensics.Client.MCP.ServerDiscovery;
using Xunit;

namespace VideoForensics.Client.MCP.Tests.ServerDiscovery
{
    public class FileServerLocationSettingsStoreTests : IDisposable
    {
        private const string CachedUrlFileName = "mcp-server-url.txt";

        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Fact]
        public void GetCachedInternetServerUrl_WhenFileMissing_ReturnsNull()
        {
            var store = new FileServerLocationSettingsStore(_root);

            Assert.Null(store.GetCachedInternetServerUrl());
        }

        [Fact]
        public void SetCachedInternetServerUrl_ThenGet_ReturnsSavedUrl()
        {
            var store = new FileServerLocationSettingsStore(_root);

            store.SetCachedInternetServerUrl("https://example.test");

            Assert.Equal("https://example.test", store.GetCachedInternetServerUrl());
        }

        [Fact]
        public void GetCachedInternetServerUrl_WhenFileEmpty_ReturnsNull()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, CachedUrlFileName), string.Empty);
            var store = new FileServerLocationSettingsStore(_root);

            Assert.Null(store.GetCachedInternetServerUrl());
        }

        [Fact]
        public void GetCachedInternetServerUrl_WhenFileWhitespaceOnly_ReturnsNull()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, CachedUrlFileName), "   \r\n\t ");
            var store = new FileServerLocationSettingsStore(_root);

            Assert.Null(store.GetCachedInternetServerUrl());
        }

        [Fact]
        public void GetCachedInternetServerUrl_WhenFileHasSurroundingWhitespace_ReturnsTrimmedUrl()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, CachedUrlFileName), "  https://example.test \r\n");
            var store = new FileServerLocationSettingsStore(_root);

            Assert.Equal("https://example.test", store.GetCachedInternetServerUrl());
        }

        [Fact]
        public void SetCachedInternetServerUrl_WhenDirectoryMissing_CreatesDirectory()
        {
            string nested = Path.Combine(_root, "nested");
            var store = new FileServerLocationSettingsStore(nested);

            store.SetCachedInternetServerUrl("https://example.test");

            Assert.True(Directory.Exists(nested));
            Assert.Equal("https://example.test", store.GetCachedInternetServerUrl());
        }
    }
}
