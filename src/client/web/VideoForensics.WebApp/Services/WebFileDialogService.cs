using Microsoft.JSInterop;

using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Web-host implementation of IFileDialogService: triggers a normal browser file download rather than
    /// a native OS save dialog (browsers can't be forced to show one) - see MauiFileDialogService for the
    /// native-dialog equivalent used by the MAUI host.
    ///
    /// Uses a JS-created &lt;a&gt; click (see wwwroot/js/export-download.js) rather than
    /// NavigationManager.NavigateTo(..., forceLoad: true) - forceLoad does a hard window.location
    /// navigation that tears down the Blazor Server SignalR circuit (and all in-memory component state)
    /// before the browser detects the response is a download and aborts the navigation, which made the
    /// Import/Export page appear to reset/blank out right after a successful export.
    /// </summary>
    public sealed class WebFileDialogService : IFileDialogService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ExportDownloadTokenStore _tokenStore;

        public WebFileDialogService(IJSRuntime jsRuntime, ExportDownloadTokenStore tokenStore)
        {
            _jsRuntime = jsRuntime;
            _tokenStore = tokenStore;
        }

        public async Task<bool> SaveExportedArchiveAsync(string sourceFilePath, string suggestedFileName, CancellationToken cancellationToken)
        {
            var token = _tokenStore.CreateToken(sourceFilePath);
            var url = $"api/export-download/{token}?fileName={Uri.EscapeDataString(suggestedFileName)}";
            await _jsRuntime.InvokeVoidAsync("vfExportDownload.trigger", cancellationToken, url);
            return true;
        }
    }
}
