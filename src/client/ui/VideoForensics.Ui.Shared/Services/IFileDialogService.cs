namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Hands a freshly-exported backup archive off to the user via each host's native "save as"
    /// experience: a real OS save dialog in the MAUI desktop host, a browser-triggered download in the
    /// Web host. Registered differently per host in MauiProgram.cs / Program.cs, the same pattern already
    /// used for IBlazorRenderModeProvider in this project.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Saves the file at <paramref name="sourceFilePath"/> (already written to local/server disk by
        /// the export step) to a user-chosen destination, suggesting <paramref name="suggestedFileName"/>
        /// as the file name. Returns false if the user cancelled.
        /// </summary>
        Task<bool> SaveExportedArchiveAsync(string sourceFilePath, string suggestedFileName, CancellationToken cancellationToken);
    }
}
