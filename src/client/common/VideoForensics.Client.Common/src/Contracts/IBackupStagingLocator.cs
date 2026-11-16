namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Says where a backup export is staged before the user saves it through the host's save dialog.
    /// Each host decides the folder, so the Ui.Shared page does not read the user's documents folder itself.
    /// </summary>
    public interface IBackupStagingLocator
    {
        /// <summary>Returns the folder to write the export to. The folder is created if it does not exist.</summary>
        Task<string> GetExportDirectoryAsync(CancellationToken ct);
    }
}
