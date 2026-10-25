using System.Security;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <inheritdoc cref="IDirectoryBrowserService"/>
    public sealed class DirectoryBrowserService : IDirectoryBrowserService
    {
        private readonly IFileSystemRootProvider _rootProvider;

        /// <param name="rootProvider">Supplies the platform's filesystem roots (drives on Windows, <c>/</c> on POSIX).</param>
        public DirectoryBrowserService(IFileSystemRootProvider rootProvider)
        {
            _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        }

        public IReadOnlyList<DirectoryEntry> GetRoots()
        {
            return _rootProvider.GetRoots()
                .Select(root => new DirectoryEntry(root.Name, root.Path))
                .ToList();
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
