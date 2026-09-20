namespace VideoForensics.Providers.Core
{
    /// <summary>
    /// Resolves the path to ffmpeg or ffprobe executables with precedence: explicit config > bundled binary next to app > bare name on PATH.
    /// </summary>
    public static class FfmpegPathResolver
    {
        /// <summary>
        /// Resolves the path to an ffmpeg/ffprobe executable.
        /// Precedence: explicit config path (if provided) > bundled binary next to app (if it exists) > bare executable name (resolved via PATH).
        /// </summary>
        /// <param name="configuredPath">Explicit path to the executable from configuration. If not null and not whitespace, returned as-is.</param>
        /// <param name="executableBaseName">Base name of the executable (e.g., "ffmpeg" or "ffprobe") without platform-specific extensions.</param>
        /// <returns>
        /// The resolved path to the executable: either the configured path, the full path to a bundled binary if found,
        /// or the bare executable name to be resolved by the OS via PATH lookup.
        /// </returns>
        public static string Resolve(string? configuredPath, string executableBaseName)
        {
            // If a path is explicitly configured, use it as-is
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            // Build the candidate path for a bundled binary next to the app
            var executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;
            var candidatePath = Path.Combine(AppContext.BaseDirectory, executableName);

            // If the bundled binary exists, return its full path
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            // Otherwise, return the bare name to be resolved via PATH
            return executableBaseName;
        }
    }
}
