namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Resolves the path to ffmpeg or ffprobe executables with precedence: explicit config > configured bin directory > bundled binary next to app > bare name on PATH.
    /// </summary>
    public static class FfmpegPathResolver
    {
        /// <summary>
        /// Resolves the path to an ffmpeg/ffprobe executable.
        /// Precedence: explicit config path (if not whitespace) > executable inside the configured bin directory (if it exists) > bundled binary next to app (if it exists) > bare executable name (resolved via PATH).
        /// </summary>
        /// <param name="configuredPath">Explicit path to the executable from configuration. If not null and not whitespace, returned as-is.</param>
        /// <param name="executableBaseName">Base name of the executable (e.g., "ffmpeg" or "ffprobe") without platform-specific extensions.</param>
        /// <param name="binDirectory">Configured directory holding ffmpeg/ffprobe (IForensicsConfiguration.FfmpegBinDirectory). Used only if the executable exists in it.</param>
        /// <returns>
        /// The resolved path to the executable: either the configured path, the full path inside the bin directory or next to the app if found,
        /// or the bare executable name to be resolved by the OS via PATH lookup.
        /// </returns>
        public static string Resolve(string? configuredPath, string executableBaseName, string? binDirectory = null)
        {
            return Resolve(configuredPath, executableBaseName, binDirectory, FfmpegBinDirectoryLocator.DiscoverDefaultExecutable);
        }

        /// <summary>
        /// Resolves the path to an ffmpeg/ffprobe executable, with a registry PATH discovery fallback.
        /// Precedence: explicit config path (if not whitespace) > executable inside the configured bin directory (if it exists) > bundled binary next to app (if it exists) > discovered path from <paramref name="discover"/> (if non-null) > bare executable name.
        /// </summary>
        /// <param name="configuredPath">Explicit path to the executable from configuration. If not null and not whitespace, returned as-is.</param>
        /// <param name="executableBaseName">Base name of the executable (e.g., "ffmpeg" or "ffprobe") without platform-specific extensions.</param>
        /// <param name="binDirectory">Configured directory holding ffmpeg/ffprobe (IForensicsConfiguration.FfmpegBinDirectory). Used only if the executable exists in it.</param>
        /// <param name="discover">Fallback lookup by executable base name, invoked only when no explicit, bin, or bundled path exists. Returns the full path, or null if not found. Each lookup is a separate call so the stale process PATH is not relied on.</param>
        /// <returns>
        /// The resolved path to the executable: the configured path, a full path from the bin directory, next to the app, or from <paramref name="discover"/>,
        /// or the bare executable name as a last resort (resolved by the OS via the process PATH, which may be stale).
        /// </returns>
        public static string Resolve(string? configuredPath, string executableBaseName, string? binDirectory, Func<string, string?> discover)
        {
            // If a path is explicitly configured, use it as-is
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            // Build the executable file name for the current platform
            string executableName = OperatingSystem.IsWindows()
                ? executableBaseName + ".exe"
                : executableBaseName;

            // Configured bin directory wins over the app directory when the executable is present there
            if (!string.IsNullOrWhiteSpace(binDirectory))
            {
                string binCandidate = Path.Combine(binDirectory, executableName);
                if (File.Exists(binCandidate))
                {
                    return binCandidate;
                }
            }

            // Build the candidate path for a bundled binary next to the app
            string candidatePath = Path.Combine(AppContext.BaseDirectory, executableName);

            // If the bundled binary exists, return its full path
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            // Discover on the registry PATHs so a fresh install is found without a process restart
            string? discovered = discover(executableBaseName);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                return discovered;
            }

            // Otherwise, return the bare name to be resolved via the (possibly stale) process PATH
            return executableBaseName;
        }
    }
}
