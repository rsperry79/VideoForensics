using Microsoft.AspNetCore.Components;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Whether the current host supports ASP.NET Core Blazor render modes. VideoForensics.WebApp is a
    /// real ASP.NET Core host with InteractiveServer components (SignalR circuits) wired up, so
    /// MainLayout's &lt;RadzenComponents&gt; needs @rendermode="InteractiveServer" there. MAUI's
    /// BlazorWebView renders entirely through its own native IPC channel and has no render-mode
    /// infrastructure at all - passing ANY @rendermode throws "the current platform does not support
    /// the ServerRenderMode" the instant MainLayout renders. Each host registers its own
    /// implementation so the one shared MainLayout.razor works on both.
    /// </summary>
    public interface IBlazorRenderModeProvider
    {
        IComponentRenderMode? InteractiveServerOrNull { get; }
    }
}
