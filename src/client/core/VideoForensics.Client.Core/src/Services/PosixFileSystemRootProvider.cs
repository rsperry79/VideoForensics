using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Filesystem root provider for POSIX hosts: a single <c>/</c> root, since POSIX has no drive concept.
    /// </summary>
    public sealed class PosixFileSystemRootProvider : IFileSystemRootProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<FileSystemRoot> GetRoots()
        {
            return new[] { new FileSystemRoot("/", "/") };
        }
    }
}
