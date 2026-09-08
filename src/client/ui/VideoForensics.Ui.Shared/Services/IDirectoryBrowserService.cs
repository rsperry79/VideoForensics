namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>A single directory entry surfaced by <see cref="IDirectoryBrowserService"/>.</summary>
    public sealed record DirectoryEntry(string Name, string FullPath);

    /// <summary>
    /// Lists local filesystem directories for the in-app folder-browser dialog. Neither host's own file
    /// dialogs (a browser's file input, or a native OS picker) can hand back a real, absolute filesystem
    /// path for an arbitrary folder in every hosting scenario this app runs in, so folder selection goes
    /// through this in-app browser instead.
    /// </summary>
    public interface IDirectoryBrowserService
    {
        /// <summary>Returns the filesystem roots (drives on Windows) to start browsing from.</summary>
        IReadOnlyList<DirectoryEntry> GetRoots();

        /// <summary>Returns the immediate subdirectories of <paramref name="path"/>, or an empty list if it
        /// can't be enumerated (e.g. access denied).</summary>
        IReadOnlyList<DirectoryEntry> GetSubdirectories(string path);
    }
}
