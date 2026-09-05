using Microsoft.AspNetCore.Components;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>Default for any BlazorWebView-hosted client (MAUI) - see <see cref="IBlazorRenderModeProvider"/>.</summary>
    public sealed class NullBlazorRenderModeProvider : IBlazorRenderModeProvider
    {
        public IComponentRenderMode? InteractiveServerOrNull => null;
    }
}
