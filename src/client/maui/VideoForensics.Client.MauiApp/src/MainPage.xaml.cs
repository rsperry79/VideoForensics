#if WINDOWS
using Microsoft.AspNetCore.Components.WebView;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Platform.Windows;
#endif

namespace VideoForensics.Client.MauiApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
#if WINDOWS
        blazorWebView.BlazorWebViewInitializing += OnBlazorWebViewInitializing;
#endif
    }

#if WINDOWS
    /// <summary>
    /// Points WebView2 at the Fixed Version runtime when one is present. Without it, WebView2 looks up the
    /// Evergreen registration, which can be stale on this machine and hides the installed runtime.
    /// </summary>
    private void OnBlazorWebViewInitializing(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        string? folder = WebView2FixedRuntimeLocator.Resolve(WebView2FixedRuntimeLocator.DefaultFolder);
        if (folder is null)
        {
            StartupTrace.Mark("webview2: no fixed-version runtime found, using installed Evergreen runtime");
            return;
        }

        e.BrowserExecutableFolder = folder;
        StartupTrace.Mark($"webview2: using fixed-version runtime at {folder}");
    }
#endif
}
