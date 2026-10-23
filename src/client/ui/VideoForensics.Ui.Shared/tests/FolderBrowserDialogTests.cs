
using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Dialogs;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class FolderBrowserDialog_Tests : BunitContext
{
    private static readonly DirectoryEntry Root = new("C:\\", "C:\\");
    private static readonly DirectoryEntry Child = new("Videos", "C:\\Videos");

    private readonly Mock<IDirectoryBrowserService> _browser = new();

    public FolderBrowserDialog_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _ = _browser.Setup(b => b.GetRoots()).Returns(new[] { Root });
        _ = _browser.Setup(b => b.GetSubdirectories("C:\\")).Returns(new[] { Child });
        _ = _browser.Setup(b => b.GetSubdirectories("C:\\Videos")).Returns(Array.Empty<DirectoryEntry>());
        _ = Services.AddSingleton(_browser.Object);
    }

    private IRenderedComponent<FolderBrowserDialog> RenderDialog(Action<string?> onClosed)
    {
        return Render<FolderBrowserDialog>(p => p.Add(d => d.OnClosed,
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(this, onClosed)));
    }

    [Fact]
    public void FolderBrowserDialog_Initial_ListsRootsAndDisablesSelect()
    {
        IRenderedComponent<FolderBrowserDialog> cut = RenderDialog(_ => { });

        Assert.Contains("C:\\", cut.Find("[data-testid='folder-entry']").TextContent);
        Assert.True(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
    }

    [Fact]
    public void FolderBrowserDialog_ClickEntry_NavigatesIntoAndEnablesSelect()
    {
        IRenderedComponent<FolderBrowserDialog> cut = RenderDialog(_ => { });

        cut.Find("[data-testid='folder-entry']").Click();

        Assert.Contains("Videos", cut.Markup);
        Assert.False(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
        _browser.Verify(b => b.GetSubdirectories("C:\\"), Times.Once);
    }

    [Fact]
    public void FolderBrowserDialog_Select_InvokesOnClosedWithCurrentPath()
    {
        string? result = "unset";
        IRenderedComponent<FolderBrowserDialog> cut = RenderDialog(p => result = p);

        cut.Find("[data-testid='folder-entry']").Click();
        cut.Find("[data-testid='folder-select']").Click();

        Assert.Equal("C:\\", result);
    }

    [Fact]
    public void FolderBrowserDialog_Cancel_InvokesOnClosedWithNull()
    {
        string? result = "unset";
        IRenderedComponent<FolderBrowserDialog> cut = RenderDialog(p => result = p);

        cut.Find("[data-testid='folder-cancel']").Click();

        Assert.Null(result);
    }

    [Fact]
    public void FolderBrowserDialog_NavigateUpFromRoot_ReturnsToRootList()
    {
        IRenderedComponent<FolderBrowserDialog> cut = RenderDialog(_ => { });

        cut.Find("[data-testid='folder-entry']").Click();
        cut.Find("[data-testid='folder-up']").Click();

        Assert.True(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='folder-up']"));
    }
}