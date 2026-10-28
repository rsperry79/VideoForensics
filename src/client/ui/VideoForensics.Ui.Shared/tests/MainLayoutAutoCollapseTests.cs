
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The left nav collapses on /media without changing the saved preference. Leaving the page restores the pane
/// to the saved preference, and a toggle on /media only flips the pane for that visit.
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

    [Fact]
    public void MainLayout_MediaPage_CollapsesLeftNavWhenPrefIsExpanded()
    {
        // Arrange
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");

        // Act
        _ = Render<MainLayout>();

        // Assert
        Assert.Equal(1, CountInvocations(CollapseIdentifier));
    }

    [Fact]
    public async Task MainLayout_LeavingMediaWithExpandedPref_ExpandsLeftNav()
    {
        // Arrange - start on /media, so the pane is auto-collapsed
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act - leave /media
        NavigateTo("http://localhost/");
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert
        Assert.Equal(1, CountInvocations(ExpandIdentifier));
    }

    [Fact]
    public async Task MainLayout_LeavingMediaWithCollapsedPref_KeepsLeftNavCollapsed()
    {
        // Arrange - the saved preference is collapsed, so leaving /media must not expand the pane
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
    public async Task MainLayout_ToggleOnMediaPage_ExpandsPaneWithoutSavingPref()
    {
        // Arrange - auto-collapsed on /media with an expanded saved preference
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("false");
        NavigateTo("http://localhost/media");
        IRenderedComponent<MainLayout> component = Render<MainLayout>();

        // Act - one click on the hamburger
        component.Find(".left-nav-toggle").Click();
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert - the pane expanded once, and no preference was written
        Assert.Equal(1, CountInvocations(ExpandIdentifier));
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
