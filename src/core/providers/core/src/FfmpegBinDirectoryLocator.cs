namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Finds the directory that holds both ffmpeg and ffprobe, so the app can run them without relying on a stale process PATH.
    /// </summary>
    public static class FfmpegBinDirectoryLocator
    {
        /// <summary>
        /// Returns the first candidate directory containing both ffmpeg and ffprobe executables, or null if none does.
        /// Null and whitespace candidates are skipped.
        /// </summary>
        public static string? FindBinDirectory(IEnumerable<string?> candidateDirectories)
        {
            var ext = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            foreach (var dir in candidateDirectories)
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(dir, "ffmpeg" + ext)) && File.Exists(Path.Combine(dir, "ffprobe" + ext)))
                {
                    return dir;
                }
            }

            return null;
        }

        /// <summary>
        /// Yields the app base directory first, then each PATH entry in order. Empty entries are skipped and surrounding quotes are trimmed.
        /// Pure function (no environment access) so it can be tested with arbitrary inputs.
        /// </summary>
        public static IEnumerable<string> DefaultCandidates(string appBaseDirectory, string? pathVariable)
        {
            yield return appBaseDirectory;

            if (string.IsNullOrEmpty(pathVariable))
            {
                yield break;
            }

            foreach (var raw in pathVariable.Split(Path.PathSeparator))
            {
                var entry = raw.Trim().Trim('"').Trim();
                if (entry.Length > 0)
                {
                    yield return entry;
                }
            }
        }

        /// <summary>Discovers the bin directory from the app base directory and then the process PATH.</summary>
        public static string? DiscoverDefault() =>
            FindBinDirectory(DefaultCandidates(AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH")));
    }
}
