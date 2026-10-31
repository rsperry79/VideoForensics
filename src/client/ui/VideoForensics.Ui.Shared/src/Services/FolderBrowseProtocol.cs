using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>
    /// Serves the folder-only subset of the Syncfusion FileManager AJAX protocol on top of
    /// <see cref="IDirectoryBrowserService"/>. Only <c>read</c> (list child folders) and <c>create</c>
    /// are handled; every other action is rejected, so the picker can never delete, move, copy or
    /// download files even if the component is asked to.
    /// </summary>
    /// <remarks>
    /// The component builds each request path from the <c>FilterPath</c> and <c>Name</c> of the folder
    /// it was given, so paths are relative to a virtual root whose children are the filesystem roots
    /// (drives on Windows). Example: <c>/C:/Videos/</c> is <c>C:\Videos</c>. Shared by the web host
    /// (in-process) and the paired server endpoint, so both enforce the same rules.
    /// </remarks>
    public static class FolderBrowseProtocol
    {
        /// <summary>The route the paired server exposes this protocol on.</summary>
        public const string Route = "/api/v1/filesystem/fileoperations";

        private const string Separator = "/";
        private const string FolderType = "Folder";

        /// <summary>Handles one request and returns the response the component expects.</summary>
        public static FolderBrowseResponseDto Handle(FolderBrowseRequestDto request, IDirectoryBrowserService browser)
        {
            IReadOnlyList<DirectoryEntry> roots = browser.GetRoots();
            return request.Action switch
            {
                "read" => Read(request.Path, roots, browser),
                "create" => Create(request, roots, browser),
                _ => Reject("not-supported", $"Action '{request.Action}' is not allowed when selecting a folder."),
            };
        }

        private static FolderBrowseResponseDto Read(string? path, IReadOnlyList<DirectoryEntry> roots, IDirectoryBrowserService browser)
        {
            IReadOnlyList<string> segments = Segments(path);
            if (segments.Count == 0)
            {
                // The virtual root lists the filesystem roots themselves; there is no real folder to list.
                IReadOnlyList<FolderBrowseEntryDto> rootEntries = roots.Select(root => ToEntry(root.Name, Separator, root.FullPath)).ToList();
                return new FolderBrowseResponseDto(ToEntry(string.Empty, Separator, string.Empty), rootEntries, null);
            }

            if (!TryResolve(segments, roots, out string? folder) || folder is null)
            {
                return Reject("invalid-path", "The folder does not exist or is outside the allowed roots.");
            }

            IReadOnlyList<DirectoryEntry> children = browser.GetSubdirectories(folder);
            string childFilterPath = FilterPathFor(segments);
            IReadOnlyList<FolderBrowseEntryDto> childEntries = children.Select(child => ToEntry(child.Name, childFilterPath, child.FullPath)).ToList();
            FolderBrowseEntryDto cwd = ToEntry(segments[^1], FilterPathFor(segments.Take(segments.Count - 1)), folder);
            return new FolderBrowseResponseDto(cwd, childEntries, null);
        }

        private static FolderBrowseResponseDto Create(FolderBrowseRequestDto request, IReadOnlyList<DirectoryEntry> roots, IDirectoryBrowserService browser)
        {
            IReadOnlyList<string> segments = Segments(request.Path);

            // Creating at the virtual root would mean creating a drive, which is never allowed.
            if (segments.Count == 0 || !TryResolve(segments, roots, out string? parent) || parent is null)
            {
                return Reject("invalid-path", "Folders can only be created inside a drive or mount point.");
            }

            string? created = browser.CreateSubdirectory(parent, request.Name ?? string.Empty);
            if (created is null)
            {
                return Reject("create-failed", "The folder could not be created. The name may be invalid or access may be denied.");
            }

            return new FolderBrowseResponseDto(null, new[] { ToEntry(Path.GetFileName(created), FilterPathFor(segments), created) }, null);
        }

        /// <summary>
        /// Splits a component path into its folder names. Empty segments and separators are dropped, so
        /// <c>/C:\/Videos/</c> becomes <c>["C:", "Videos"]</c>. Empty or null paths give no segments.
        /// </summary>
        private static IReadOnlyList<string> Segments(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<string>();
            }

            return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim('\\'))
                .Where(segment => segment.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Maps relative segments to an absolute folder under one of the roots. Returns false for an unknown
        /// root or for a segment that could escape the root (<c>.</c>, <c>..</c>, or invalid name characters).
        /// A null <paramref name="folder"/> with true means the virtual root.
        /// </summary>
        private static bool TryResolve(IReadOnlyList<string> segments, IReadOnlyList<DirectoryEntry> roots, out string? folder)
        {
            folder = null;
            if (segments.Count == 0)
            {
                return true;
            }

            DirectoryEntry? root = roots.FirstOrDefault(candidate => RootSegment(candidate.Name) == segments[0]);
            if (root is null)
            {
                return false;
            }

            string resolved = root.FullPath;
            for (int i = 1; i < segments.Count; i++)
            {
                string segment = segments[i];
                if (segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }

                resolved = Path.Combine(resolved, segment);
            }

            folder = resolved;
            return true;
        }

        // A root such as "C:\" is shown and addressed as "C:". The POSIX root "/" has no name once the
        // separators are trimmed, so it is addressed as "root" instead.
        private static string RootSegment(string rootName)
        {
            string trimmed = rootName.Trim('\\', '/');
            return trimmed.Length == 0 ? "root" : trimmed;
        }

        private static string FilterPathFor(IEnumerable<string> segments)
        {
            return Separator + string.Concat(segments.Select(segment => segment + Separator));
        }

        // HasChild is always true: the picker lists children on demand, and checking each folder for
        // subfolders would cost a directory scan per entry.
        private static FolderBrowseEntryDto ToEntry(string name, string filterPath, string absolutePath)
        {
            return new FolderBrowseEntryDto(name, filterPath, true, false, FolderType, absolutePath);
        }

        private static FolderBrowseResponseDto Reject(string code, string message)
        {
            return new FolderBrowseResponseDto(null, Array.Empty<FolderBrowseEntryDto>(), new FolderBrowseErrorDto(code, message));
        }
    }
}
