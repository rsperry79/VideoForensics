using System.Security;

namespace VideoForensics.Ui.Shared.Services
{
    /// <inheritdoc cref="IDirectoryBrowserService"/>
    public sealed class DirectoryBrowserService : IDirectoryBrowserService
    {
        public IReadOnlyList<DirectoryEntry> GetRoots()
        {
            var roots = new List<DirectoryEntry>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                roots.Add(new DirectoryEntry(drive.Name, drive.RootDirectory.FullName));
            }

            return roots;
        }

        public IReadOnlyList<DirectoryEntry> GetSubdirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path)
                    .Select(dir => new DirectoryEntry(Path.GetFileName(dir), dir))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
            {
                return Array.Empty<DirectoryEntry>();
            }
        }
    }
}
