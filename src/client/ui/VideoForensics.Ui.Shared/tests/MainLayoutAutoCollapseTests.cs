
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The media pages no longer auto-collapse the left nav (it hosts the review view nav). The saved preference
/// drives the pane on every page, and a toggle on /media saves it like anywhere else.
/// </summary>
public class MainLayoutAutoCollapseTests : SimpleModeLayoutTestBase
{
    // Syncfusion's Splitter JS identifiers for CollapseAsync / ExpandAsync.
    private const string CollapseIdentifier = "sfBlazor.Splitter.collapse";
    private const string ExpandIdentifier = "sfBlazor.Splitter.expand";
    private const string LeftNavKey = "vf.layout.leftNavCollapsed";
    private const string SetPrefIdentifier = "vfLayoutPrefs.set";

    public MainLayoutAutoCollapseTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("http://localhost/media")]
    [InlineData("http://localhost/media/review")]
    public void MainLayout_MediaPage_DoesNotCollapseLeftNavWhenPrefIsExpanded(string uri)
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo(uri);

        // Act
        _ = Render<MainLayout>();

        // Assert
        Assert.Equal(0, CountInvocations(CollapseIdentifier));
    }

    [Theory]
    [InlineData("http://localhost/media")]
    [InlineData("http://localhost/media/review")]
    [InlineData("http://localhost/live-view")]
    public void MainLayout_MediaPage_KeepsLeftNavOpenWhenPrefIsCollapsed(string uri)
    {
        // Arrange - the media menu lives in the left pane, so the pane opens even with a collapsed saved preference
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        NavigateTo(uri);

        // Act
        _ = Render<MainLayout>();

        // Assert - the pane stays open; it is never collapsed on these pages
        Assert.Equal(0, CountInvocations(CollapseIdentifier));
    }

    [Fact]
    public async Task MainLayout_LeavingMediaWithExpandedPref_DoesNotTouchThePane()
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act
        NavigateTo("http://localhost/");
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert
        Assert.Equal(0, CountInvocations(CollapseIdentifier));
        Assert.Equal(0, CountInvocations(ExpandIdentifier));
    }

    [Fact]
    public async Task MainLayout_LeavingMediaWithCollapsedPref_KeepsLeftNavCollapsed()
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act
        NavigateTo("http://localhost/");
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert
        Assert.Equal(0, CountInvocations(ExpandIdentifier));
    }

    [Fact]
    public async Task MainLayout_ToggleOnMediaPage_CollapsesPaneWithoutSavingPref()
    {
        // Arrange - expanded saved preference on /media
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act - one click on the hamburger
        component.Find(".left-nav-toggle").Click();
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert - the pane collapses for this visit only; the saved preference is untouched
        Assert.Equal(1, CountInvocations(CollapseIdentifier));
        Assert.Equal(0, CountInvocations(SetPrefIdentifier));
    }

    private void NavigateTo(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    private int CountInvocations(string identifier)
    {
        return JSInterop.Invocations.Count(i => i.Identifier == identifier);
    }
}
