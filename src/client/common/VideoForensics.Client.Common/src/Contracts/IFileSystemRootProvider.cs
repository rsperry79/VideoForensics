namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// A filesystem root that a folder browser can start from (a drive letter on Windows, <c>/</c> on POSIX).
    /// </summary>
    /// <param name="Name">Display label for the root, e.g. <c>C:\</c> or <c>/</c>.</param>
    /// <param name="Path">Absolute path of the root directory, suitable for passing to directory enumeration.</param>
    public sealed record FileSystemRoot(string Name, string Path);

    /// <summary>
    /// Supplies the platform's filesystem roots. Platform-specific: Windows enumerates ready drives, POSIX hosts
    /// expose a single <c>/</c> root. Kept behind this seam so shared UI code does not depend on drive enumeration.
    /// </summary>
    public interface IFileSystemRootProvider
    {
        /// <summary>Returns the filesystem roots available on this host, in display order.</summary>
        IReadOnlyList<FileSystemRoot> GetRoots();
    }
}
