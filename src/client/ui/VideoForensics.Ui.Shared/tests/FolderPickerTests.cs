using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Dialogs;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class FolderPicker_Tests : BunitContext
{
    private readonly Mock<IDirectoryBrowserService> _browser = new();

    public FolderPicker_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _ = _browser.Setup(b => b.GetRoots()).Returns(new[] { new DirectoryEntry("C:\\", "C:\\") });
        _ = Services.AddSingleton(_browser.Object);
        _ = Services.AddScoped<IFolderBrowserEndpoint, InProcessFolderBrowserEndpoint>();

        // SfFileManager reads the renderer to decide static vs interactive; the web host is interactive server.
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedComponent<FolderPicker> RenderPicker(Action<string?> onClosed)
    {
        return Render<FolderPicker>(p => p.Add(d => d.OnClosed,
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(this, onClosed)));
    }

    [Fact]
    public void FolderPicker_Initial_SelectIsDisabled()
    {
        IRenderedComponent<FolderPicker> cut = RenderPicker(_ => { });

        Assert.True(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
    }

    [Fact]
    public void FolderPicker_Cancel_InvokesOnClosedWithNull()
    {
        string? result = "unset";
        IRenderedComponent<FolderPicker> cut = RenderPicker(p => result = p);

        cut.Find("[data-testid='folder-cancel']").Click();

        Assert.Null(result);
    }
}
