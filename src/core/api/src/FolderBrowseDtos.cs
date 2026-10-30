namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Request body for the folder-only file manager endpoint (<c>/api/v1/filesystem/fileoperations</c>).
    /// Field names follow the Syncfusion FileManager AJAX protocol so the component can post to it directly.
    /// Only <c>read</c> and <c>create</c> are accepted; every other action is rejected by the server.
    /// </summary>
    /// <param name="Action">The file manager operation name (<c>read</c> or <c>create</c>).</param>
    /// <param name="Path">The folder the operation applies to, as a relative path from the virtual root (e.g. <c>/C:\/Videos/</c>).</param>
    /// <param name="Name">The new folder name for <c>create</c>; unused by <c>read</c>.</param>
    public sealed record FolderBrowseRequestDto(string Action, string? Path, string? Name);

    /// <summary>Response body for the folder-only file manager endpoint.</summary>
    /// <param name="Cwd">The folder that was listed, or null when listing the virtual root.</param>
    /// <param name="Files">The child folders of <paramref name="Cwd"/>. Files are never returned.</param>
    /// <param name="Error">Set when the request was rejected or failed; otherwise null.</param>
    public sealed record FolderBrowseResponseDto(
        FolderBrowseEntryDto? Cwd,
        IReadOnlyList<FolderBrowseEntryDto> Files,
        FolderBrowseErrorDto? Error
    );

    /// <summary>One folder in a <see cref="FolderBrowseResponseDto"/>.</summary>
    /// <param name="Name">The folder name shown to the user.</param>
    /// <param name="FilterPath">The relative path of the folder's parent, with a trailing separator.</param>
    /// <param name="HasChild">True when the folder has at least one child folder.</param>
    /// <param name="IsFile">Always false; present because the component expects the field.</param>
    /// <param name="Type">The entry type shown in the component (<c>Folder</c>).</param>
    /// <param name="Id">The folder's absolute path on the server. The picker returns this value when a folder is chosen.</param>
    public sealed record FolderBrowseEntryDto(
        string Name,
        string FilterPath,
        bool HasChild,
        bool IsFile,
        string Type,
        string Id
    );

    /// <summary>Error details for a rejected or failed folder browse request.</summary>
    /// <param name="Code">A short machine-readable code (e.g. <c>not-supported</c>, <c>invalid-path</c>).</param>
    /// <param name="Message">A message shown to the user.</param>
    public sealed record FolderBrowseErrorDto(string Code, string Message);
}
