using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>Default for VideoForensics.Client.WebApp, which has InteractiveServer components wired up - see <see cref="IBlazorRenderModeProvider"/>.</summary>
    public sealed class InteractiveServerBlazorRenderModeProvider : IBlazorRenderModeProvider
    {
        public IComponentRenderMode? InteractiveServerOrNull => RenderMode.InteractiveServer;
    }
}
