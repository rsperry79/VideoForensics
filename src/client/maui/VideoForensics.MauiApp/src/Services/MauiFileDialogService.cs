using CommunityToolkit.Maui.Storage;

using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.MauiApp.Services
{
    /// <summary>
    /// MAUI implementation of <see cref="IFileDialogService"/> using the native OS "Save As" dialog
    /// via <see cref="IFileSaver"/> (provided by CommunityToolkit.Maui).
    /// </summary>
    public sealed class MauiFileDialogService : IFileDialogService
    {
        private readonly IFileSaver _fileSaver;

        /// <summary>
        /// Initializes a new instance of <see cref="MauiFileDialogService"/>.
        /// </summary>
        /// <param name="fileSaver">The <see cref="IFileSaver"/> service (injected by DI).</param>
        public MauiFileDialogService(IFileSaver fileSaver)
        {
            _fileSaver = fileSaver ?? throw new ArgumentNullException(nameof(fileSaver));
        }

        /// <inheritdoc/>
        public async Task<bool> SaveExportedArchiveAsync(string sourceFilePath, string suggestedFileName, CancellationToken cancellationToken)
        {
            try
            {
                using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                FileSaverResult result = await _fileSaver.SaveAsync(suggestedFileName, sourceStream, cancellationToken);
                return result.IsSuccessful;
            }
            catch
            {
                return false;
            }
        }
    }
}
