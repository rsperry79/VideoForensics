namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Locates an extracted Fixed Version WebView2 runtime so the BlazorWebView does not depend on the Evergreen registration.
    /// </summary>
    public static class WebView2FixedRuntimeLocator
    {
        /// <summary>
        /// Executable that must exist inside a Fixed Version runtime folder.
        /// </summary>
        public const string RuntimeExecutableName = "msedgewebview2.exe";

        /// <summary>
        /// Folder holding an extracted Fixed Version WebView2 runtime. The BlazorWebView points at it so the app does not depend on the Evergreen registration.
        /// </summary>
        public static string DefaultFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VideoForensics",
            "WebView2FixedVersion");

        /// <summary>
        /// Returns the folder when it contains the runtime executable, otherwise null so the caller falls back to the installed Evergreen runtime.
        /// </summary>
        /// <param name="folder">Candidate Fixed Version runtime folder.</param>
        /// <param name="fileExists">File existence check, injectable for tests.</param>
        public static string? Resolve(string folder, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }

            return fileExists(Path.Combine(folder, RuntimeExecutableName)) ? folder : null;
        }

        /// <summary>
        /// Returns the folder when it contains the runtime executable, otherwise null so the caller falls back to the installed Evergreen runtime.
        /// </summary>
        /// <param name="folder">Candidate Fixed Version runtime folder.</param>
        public static string? Resolve(string folder) => Resolve(folder, File.Exists);
    }
}
