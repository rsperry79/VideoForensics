namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>
    /// Resolves manifest paths under a root folder. The service applies packages as SYSTEM, so a path that escapes
    /// the root, or that passes through a symlink or junction, would let a package write anywhere on disk.
    /// </summary>
    internal static class UpdatePathGuard
    {
        /// <summary>
        /// Resolves <paramref name="relativePath"/> under <paramref name="root"/>. Returns false for rooted paths,
        /// ".." segments, paths that resolve outside the root, and paths that pass through an existing reparse point.
        /// </summary>
        public static bool TryResolve(string root, string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            {
                return false;
            }

            string[] segments = relativePath.Split(['/', '\\'], StringSplitOptions.None);
            if (segments.Any(s => s.Length == 0 || s == "." || s == ".."))
            {
                return false;
            }

            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(rootFull, Path.Combine(segments)));
            if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string current = rootFull;
            foreach (string segment in segments)
            {
                current = Path.Combine(current, segment);
                if (IsReparsePoint(current))
                {
                    return false;
                }
            }

            fullPath = candidate;
            return true;
        }

        /// <summary>True when the path exists and is a symlink, junction or other reparse point.</summary>
        public static bool IsReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
    }
}
