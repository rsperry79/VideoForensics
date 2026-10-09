using System;
using System.IO;

using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// File-backed <see cref="IHardBanStateStore"/> that keeps the hard-ban expiry as UTC ticks in
    /// <c>ring_hard_ban.txt</c>. Defaults to the machine-wide application data directory.
    /// </summary>
    public sealed class FileHardBanStateStore : IHardBanStateStore
    {
        private const string FileName = "ring_hard_ban.txt";

        /// <summary>
        /// Initializes the store.
        /// </summary>
        /// <param name="directory">Directory holding the state file; null uses the application data directory.</param>
        public FileHardBanStateStore(string? directory = null)
        {
            FilePath = Path.Combine(directory ?? new PlatformDirectoryService().GetApplicationDataDirectory(), FileName);
        }

        /// <summary>Full path of the state file.</summary>
        public string FilePath { get; }

        /// <inheritdoc />
        public DateTime? Read()
        {
            try
            {
                if (File.Exists(FilePath) && long.TryParse(File.ReadAllText(FilePath).Trim(), out long ticks))
                {
                    return new DateTime(ticks, DateTimeKind.Utc);
                }
            }
            catch { }

            return null;
        }

        /// <inheritdoc />
        public void Write(DateTime untilUtc)
        {
            try
            {
                string? folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    _ = Directory.CreateDirectory(folder);
                }

                File.WriteAllText(FilePath, untilUtc.Ticks.ToString());
            }
            catch { }
        }

        /// <inheritdoc />
        public void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch { }
        }
    }
}
