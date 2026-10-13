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
        private static readonly string StorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VideoForensics");

        private static readonly string CachedUrlFilePath = Path.Combine(StorageDirectory, "mcp-server-url.txt");

        public string? GetCachedInternetServerUrl()
        {
            try
            {
                if (!File.Exists(CachedUrlFilePath))
                {
                    return null;
                }

                string content = File.ReadAllText(CachedUrlFilePath).Trim();
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
                _ = Directory.CreateDirectory(StorageDirectory);
                File.WriteAllText(CachedUrlFilePath, url);
            }
            catch
            {
                // If writing fails, log it but don't throw - the bridge can still operate
                // without persisting the URL (next startup will re-discover via mDNS)
            }
        }
    }
}
