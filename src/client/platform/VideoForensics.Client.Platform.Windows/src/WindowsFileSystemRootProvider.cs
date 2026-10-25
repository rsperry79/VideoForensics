using System.Runtime.Versioning;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Windows filesystem root provider: one root per ready drive. Drives that are not ready (empty card readers,
    /// disconnected network shares) are skipped so the folder browser only offers roots it can list.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsFileSystemRootProvider : IFileSystemRootProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<FileSystemRoot> GetRoots()
        {
            var roots = new List<FileSystemRoot>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                roots.Add(new FileSystemRoot(drive.Name, drive.RootDirectory.FullName));
            }

            return roots;
        }
    }
}
