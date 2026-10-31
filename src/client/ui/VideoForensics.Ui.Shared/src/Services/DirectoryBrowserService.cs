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

        public string? CreateSubdirectory(string parentPath, string name)
        {
            // Reject separators and other invalid characters so the name cannot escape the parent folder.
            string trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }

            try
            {
                string fullPath = Path.Combine(parentPath, trimmed);
                _ = Directory.CreateDirectory(fullPath);
                return fullPath;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
            {
                return null;
            }
        }
    }
}
