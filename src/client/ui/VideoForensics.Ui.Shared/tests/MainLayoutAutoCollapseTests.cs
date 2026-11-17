
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
    // The left pane is hidden or shown through vfLayoutPrefs.setLeftNavHidden(hidden).
    private const string SetHiddenIdentifier = "vfLayoutPrefs.setLeftNavHidden";
    private const string LeftNavKey = "vf.layout.leftNavCollapsed";
    private const string SetPrefIdentifier = "vfLayoutPrefs.set";

    public MainLayoutAutoCollapseTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // The splitter is ready in these tests, so the hide is applied on the first call.
        JSInterop.Setup<bool>(SetHiddenIdentifier, _ => true).SetResult(true);
    }

    [Theory]
    [InlineData("http://localhost/media")]
    [InlineData("http://localhost/media/review")]
    public void MainLayout_MediaPage_CollapsesLeftNavWhenPrefIsExpanded(string uri)
    {
        // Arrange: the review tabs moved to the page header, so /media collapses the left nav (U-D2)
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo(uri);

        // Act
        _ = Render<MainLayout>();

        // Assert
        Assert.Equal(1, CountHidden(true));
    }

    [Theory]
    [InlineData("http://localhost/media")]
    [InlineData("http://localhost/media/review")]
    public void MainLayout_MediaPage_StaysCollapsedWhenPrefIsCollapsed(string uri)
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        NavigateTo(uri);

        // Act
        _ = Render<MainLayout>();

        // Assert
        Assert.Equal(1, CountHidden(true));
    }

    [Fact]
    public void MainLayout_LiveViewPage_KeepsLeftNavOpenWhenPrefIsCollapsed()
    {
        // Arrange - Live view still hosts its own menu in the left pane
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        NavigateTo("http://localhost/live-view");

        // Act
        _ = Render<MainLayout>();

        // Assert
        Assert.Equal(0, CountHidden(true));
    }

    [Fact]
    public async Task MainLayout_LeavingMediaWithExpandedPref_ExpandsThePane()
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act
        NavigateTo("http://localhost/");
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert: /media collapsed the pane; leaving it brings the saved (expanded) preference back
        Assert.Equal(1, CountHidden(true));
        Assert.Equal(1, CountHidden(false));
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
        Assert.Equal(0, CountHidden(false));
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
        Assert.Equal(1, CountHidden(true));
        Assert.Equal(0, JSInterop.Invocations.Count(i => i.Identifier == SetPrefIdentifier));
    }

    private void NavigateTo(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    private int CountHidden(bool hidden)
    {
        return JSInterop.Invocations.Count(i => i.Identifier == SetHiddenIdentifier && i.Arguments.Count == 1 && i.Arguments[0] is bool value && value == hidden);
    }
}
