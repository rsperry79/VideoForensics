namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Ui.Shared.Dialogs;
using VideoForensics.Ui.Shared.Services;
using Xunit;

public class FolderBrowserDialog_Tests : BunitContext
{
    private static readonly DirectoryEntry Root = new("C:\\", "C:\\");
    private static readonly DirectoryEntry Child = new("Videos", "C:\\Videos");

    private readonly Mock<IDirectoryBrowserService> _browser = new();

    public FolderBrowserDialog_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        _browser.Setup(b => b.GetRoots()).Returns(new[] { Root });
        _browser.Setup(b => b.GetSubdirectories("C:\\")).Returns(new[] { Child });
        _browser.Setup(b => b.GetSubdirectories("C:\\Videos")).Returns(Array.Empty<DirectoryEntry>());
        Services.AddSingleton(_browser.Object);
    }

    private IRenderedComponent<FolderBrowserDialog> RenderDialog(Action<string?> onClosed) =>
        Render<FolderBrowserDialog>(p => p.Add(d => d.OnClosed,
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(this, onClosed)));

    [Fact]
    public void FolderBrowserDialog_Initial_ListsRootsAndDisablesSelect()
    {
        var cut = RenderDialog(_ => { });

        Assert.Contains("C:\\", cut.Find("[data-testid='folder-entry']").TextContent);
        Assert.True(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
    }

    [Fact]
    public void FolderBrowserDialog_ClickEntry_NavigatesIntoAndEnablesSelect()
    {
        var cut = RenderDialog(_ => { });

        cut.Find("[data-testid='folder-entry']").Click();

        Assert.Contains("Videos", cut.Markup);
        Assert.False(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
        _browser.Verify(b => b.GetSubdirectories("C:\\"), Times.Once);
    }

    [Fact]
    public void FolderBrowserDialog_Select_InvokesOnClosedWithCurrentPath()
    {
        string? result = "unset";
        var cut = RenderDialog(p => result = p);

        cut.Find("[data-testid='folder-entry']").Click();
        cut.Find("[data-testid='folder-select']").Click();

        Assert.Equal("C:\\", result);
    }

    [Fact]
    public void FolderBrowserDialog_Cancel_InvokesOnClosedWithNull()
    {
        string? result = "unset";
        var cut = RenderDialog(p => result = p);

        cut.Find("[data-testid='folder-cancel']").Click();

        Assert.Null(result);
    }

    [Fact]
    public void FolderBrowserDialog_NavigateUpFromRoot_ReturnsToRootList()
    {
        var cut = RenderDialog(_ => { });

        cut.Find("[data-testid='folder-entry']").Click();
        cut.Find("[data-testid='folder-up']").Click();

        Assert.True(cut.Find("[data-testid='folder-select']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='folder-up']"));
    }
}