using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Ui.Shared.Pages
{
    /// <summary>
    /// Formats the Current Path cell on the Storage page. Categories on the platform default show
    /// "{default label}/{category}" instead of the resolved absolute path, so the table reads as
    /// "Default/Keys" rather than a drive-specific path. Overridden categories show their real path.
    /// </summary>
    public static class StoragePathDisplay
    {
        /// <param name="status">The category status row.</param>
        /// <param name="defaultLabel">Localized prefix for platform-default locations, e.g. "Default".</param>
        public static string Format(StorageCategoryStatus status, string defaultLabel)
        {
            return status.IsDefault
                ? $"{defaultLabel}/{status.Category}"
                : status.CurrentPath;
        }
    }
}
