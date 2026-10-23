using Xunit;

namespace VideoForensics.Client.MCP.Tests.ServerDiscovery
{
    public class FileApiKeyStoreTests : IDisposable
    {
        private const string ApiKeyFileName = "mcp-api-key.txt";

        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Fact]
        public void GetApiKey_WhenFileMissing_ReturnsNull()
        {
            var store = new FileApiKeyStore(_root);

            Assert.Null(store.GetApiKey());
        }

        [Fact]
        public void SetApiKey_ThenGetApiKey_ReturnsSavedKey()
        {
            var store = new FileApiKeyStore(_root);

            store.SetApiKey("key-123");

            Assert.Equal("key-123", store.GetApiKey());
        }

        [Fact]
        public void GetApiKey_WhenFileEmpty_ReturnsNull()
        {
            _ = Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, ApiKeyFileName), string.Empty);
            var store = new FileApiKeyStore(_root);

            Assert.Null(store.GetApiKey());
        }

        [Fact]
        public void GetApiKey_WhenFileWhitespaceOnly_ReturnsNull()
        {
            _ = Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, ApiKeyFileName), "   \r\n\t ");
            var store = new FileApiKeyStore(_root);

            Assert.Null(store.GetApiKey());
        }

        [Fact]
        public void GetApiKey_WhenFileHasSurroundingWhitespace_ReturnsTrimmedKey()
        {
            _ = Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, ApiKeyFileName), "  key-456 \r\n");
            var store = new FileApiKeyStore(_root);

            Assert.Equal("key-456", store.GetApiKey());
        }

        [Fact]
        public void ClearApiKey_WhenKeySet_DeletesFile()
        {
            var store = new FileApiKeyStore(_root);
            store.SetApiKey("key-789");

            store.ClearApiKey();

            Assert.False(File.Exists(Path.Combine(_root, ApiKeyFileName)));
            Assert.Null(store.GetApiKey());
        }

        [Fact]
        public void SetApiKey_WhenDirectoryMissing_CreatesDirectory()
        {
            string nested = Path.Combine(_root, "nested");
            var store = new FileApiKeyStore(nested);

            store.SetApiKey("key-000");

            Assert.True(Directory.Exists(nested));
            Assert.Equal("key-000", store.GetApiKey());
        }
    }
}
