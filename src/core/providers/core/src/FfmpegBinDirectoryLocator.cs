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
        /// Yields the app base directory first, then each PATH entry in order. Empty entries are skipped, surrounding quotes are trimmed,
        /// and %VAR% references are expanded: a process started with an unexpanded PATH (e.g. one that still holds
        /// "%LOCALAPPDATA%\...") would otherwise never match the real directory.
        /// Pure apart from variable expansion, so it can be tested with arbitrary inputs.
        /// </summary>
        public static IEnumerable<string> DefaultCandidates(string appBaseDirectory, string? pathVariable) =>
            DefaultCandidates(appBaseDirectory, new[] { pathVariable });

        /// <summary>
        /// Like <see cref="DefaultCandidates(string, string?)"/> but over several PATH sources in order (process, user, machine).
        /// Needed because a desktop app started before a tool was installed keeps a stale process PATH; the registry PATHs are current.
        /// </summary>
        public static IEnumerable<string> DefaultCandidates(string appBaseDirectory, IEnumerable<string?> pathVariables)
        {
            yield return appBaseDirectory;

            foreach (var pathVariable in pathVariables)
            {
                if (string.IsNullOrEmpty(pathVariable))
                {
                    continue;
                }

                foreach (var raw in pathVariable.Split(Path.PathSeparator))
                {
                    var entry = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"').Trim()).Trim();
                    if (entry.Length > 0)
                    {
                        yield return entry;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the full path of <paramref name="fileName"/> (with the platform executable extension) inside the first
        /// candidate directory that contains it, or null if none does. Null and whitespace candidates are skipped.
        /// </summary>
        public static string? FindExecutable(IEnumerable<string?> candidateDirectories, string fileName)
        {
            var exeName = fileName + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);
            foreach (var dir in candidateDirectories)
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                var candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Discovers the bin directory from the app base directory, then the process, user and machine PATH.</summary>
        public static string? DiscoverDefault() =>
            FindBinDirectory(DefaultCandidates(AppContext.BaseDirectory, PathVariables()));

        /// <summary>Discovers an executable by base name from the app base directory, then the process, user and machine PATH.</summary>
        public static string? DiscoverDefaultExecutable(string fileName) =>
            FindExecutable(DefaultCandidates(AppContext.BaseDirectory, PathVariables()), fileName);

        private static string?[] PathVariables() =>
        [
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
        ];
    }
}
