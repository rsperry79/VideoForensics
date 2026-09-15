namespace VideoForensics.Mcp.ServerDiscovery
{
    /// <summary>
    /// File-based implementation of <see cref="IApiKeyStore"/> for the MCP bridge process.
    /// Persists the API key obtained via device-code pairing as a single line of plain text under
    /// %LOCALAPPDATA%\VideoForensics\mcp-api-key.txt.
    ///
    /// IMPORTANT: Uses LocalApplicationData (per-user), NOT CommonApplicationData (machine-wide).
    /// CommonApplicationData is readable by other local accounts by default, which would expose
    /// this device's long-lived credential. LocalApplicationData is isolated to the current user
    /// and not readable by other accounts, making it the correct choice for secrets.
    /// This is a deliberate difference from <see cref="FileServerLocationSettingsStore"/>, which
    /// uses CommonApplicationData for the non-sensitive cached server URL — do not "fix" this
    /// discrepancy; the difference is intentional.
    /// </summary>
    internal class FileApiKeyStore : IApiKeyStore
    {
        private static readonly string StorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoForensics");

        private static readonly string ApiKeyFilePath = Path.Combine(StorageDirectory, "mcp-api-key.txt");

        public string? GetApiKey()
        {
            try
            {
                if (!File.Exists(ApiKeyFilePath))
                {
                    return null;
                }

                string content = File.ReadAllText(ApiKeyFilePath).Trim();
                return string.IsNullOrEmpty(content) ? null : content;
            }
            catch
            {
                // If reading fails for any reason, return null and trigger re-pairing
                return null;
            }
        }

        public void SetApiKey(string apiKey)
        {
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                File.WriteAllText(ApiKeyFilePath, apiKey);
            }
            catch
            {
                // If writing fails, log it but don't throw - the bridge can still operate
                // without persisting the key (next startup will re-trigger pairing)
            }
        }

        public void ClearApiKey()
        {
            try
            {
                if (File.Exists(ApiKeyFilePath))
                {
                    File.Delete(ApiKeyFilePath);
                }
            }
            catch
            {
                // If deletion fails, ignore - next pairing attempt will overwrite it anyway
            }
        }
    }

    /// <summary>
    /// Contract for storing and retrieving the device-code pairing API key.
    /// </summary>
    internal interface IApiKeyStore
    {
        /// <summary>
        /// Retrieve the stored API key, or null if it doesn't exist or is empty.
        /// </summary>
        string? GetApiKey();

        /// <summary>
        /// Store the API key for subsequent use.
        /// </summary>
        void SetApiKey(string apiKey);

        /// <summary>
        /// Clear the stored API key (e.g., when the server rejects it with 401).
        /// </summary>
        void ClearApiKey();
    }
}
