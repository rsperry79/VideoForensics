namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Deletes partial time-lapse files ("*.building.mp4") that a crash left behind. Files live exactly three folders
    /// below the media root (root\provider\location\device\file), so the walk is bounded to that depth.
    /// </summary>
    public static class BuildingFileCleanup
    {
        private const string PartialSuffix = ".building.mp4";

        /// <summary>
        /// Deletes partial files whose last write is earlier than <paramref name="processStartUtc"/>. A file written
        /// since this process started may belong to a build running now, so it is kept.
        /// </summary>
        /// <param name="mediaRoot">The media root. A missing root returns 0.</param>
        /// <param name="processStartUtc">The UTC start of this process.</param>
        /// <returns>The number of files deleted. Files that cannot be read or deleted are skipped.</returns>
        public static int Delete(string mediaRoot, DateTime processStartUtc)
        {
            if (string.IsNullOrEmpty(mediaRoot) || !Directory.Exists(mediaRoot))
            {
                return 0;
            }

            int deleted = 0;
            foreach (string provider in ListDirectories(mediaRoot))
            {
                foreach (string location in ListDirectories(provider))
                {
                    foreach (string device in ListDirectories(location))
                    {
                        foreach (string file in ListPartialFiles(device))
                        {
                            if (TryDeleteIfOlder(file, processStartUtc))
                            {
                                deleted++;
                            }
                        }
                    }
                }
            }

            return deleted;
        }

        private static string[] ListDirectories(string parent)
        {
            try
            {
                return Directory.GetDirectories(parent);
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
        }

        private static string[] ListPartialFiles(string folder)
        {
            try
            {
                return Directory.GetFiles(folder, "*" + PartialSuffix);
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
        }

        private static bool TryDeleteIfOlder(string file, DateTime processStartUtc)
        {
            // Why: the pattern alone is not enough on Windows (8.3 short names can match loosely), so the suffix is checked again.
            if (!file.EndsWith(PartialSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(file) >= processStartUtc)
                {
                    return false;
                }

                File.Delete(file);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
