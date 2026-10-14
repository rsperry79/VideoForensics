using VideoForensics.Hosting.ServerDiscovery;

namespace VideoForensics.Mcp.ServerDiscovery
{
    /// <summary>
    /// File-based implementation of <see cref="IServerLocationSettingsStore"/> for the MCP bridge process.
    /// Since the MCP bridge has no access to MAUI Preferences API (it runs as a standalone stdio process),
    /// this implementation persists the cached Internet server URL as a single line of plain text under
    /// %ProgramData%\VideoForensics\mcp-server-url.txt.
    /// </summary>
    internal class FileServerLocationSettingsStore : IServerLocationSettingsStore
    {
        private readonly string _storageDirectory;
        private readonly string _cachedUrlFilePath;

        public FileServerLocationSettingsStore(string? storageDirectory = null)
        {
            _storageDirectory = storageDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics");
            _cachedUrlFilePath = Path.Combine(_storageDirectory, "mcp-server-url.txt");
        }

        public string? GetCachedInternetServerUrl()
        {
            try
            {
                if (!File.Exists(_cachedUrlFilePath))
                {
                    return null;
                }

                string content = File.ReadAllText(_cachedUrlFilePath).Trim();
                return string.IsNullOrEmpty(content) ? null : content;
            }
            catch
            {
                // If reading fails for any reason, return null and fall back to mDNS
                return null;
            }
        }

        public void SetCachedInternetServerUrl(string url)
        {
            try
            {
                _ = Directory.CreateDirectory(_storageDirectory);
                File.WriteAllText(_cachedUrlFilePath, url);
            }
            catch
            {
                // If writing fails, log it but don't throw - the bridge can still operate
                // without persisting the URL (next startup will re-discover via mDNS)
            }
        }
    }
}
