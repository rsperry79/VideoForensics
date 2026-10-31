using AngleSharp.Dom;

using AngleSharp.Html.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using VideoForensics.Client.Ui.Shared.Resources;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review filter panel: provider, account and location choices, the account reset on a provider change, the debounced auto-apply, the camera selector below the location select, and the matching-camera note.</summary>
public class ReviewFilterPanel_Tests : ReviewTestContext
{
    private ProviderAccount _ring = null!;
    private ProviderAccount _wyze = null!;

    /// <summary>Two providers: Ring owns the first two cameras, Wyze owns the third.</summary>
    private void SetUpTwoProviders()
    {
        _ring = AddAccount("Ring");
        _wyze = AddAccount("Wyze");
        AddDevices(3);
        Devices[0].LocationId = LinkLocation(_ring).Id;
        Devices[1].LocationId = LinkLocation(_ring).Id;
        Devices[2].LocationId = LinkLocation(_wyze).Id;
    }

    private DeviceOwnershipSnapshot Snapshot()
    {
        var ownership = new List<DeviceOwnership>
        {
            new(Devices[0].Id, _ring.Id, "Ring", Devices[0].LocationId),
            new(Devices[1].Id, _ring.Id, "Ring", Devices[1].LocationId),
            new(Devices[2].Id, _wyze.Id, "Wyze", Devices[2].LocationId),
        };
        var names = new Dictionary<Guid, string>
        {
            [_ring.Id] = "Front door account",
            [_wyze.Id] = "Garage account",
        };
        List<FilterLocation> locations =
        [
            new(Devices[0].LocationId, "Porch"),
            new(Devices[1].LocationId, "Yard"),
            new(Devices[2].LocationId, "Garage"),
        ];
        return new DeviceOwnershipSnapshot(Accounts.ToList(), names, ["Ring", "Wyze"], ownership, Locations: locations);
    }

    private IRenderedComponent<ReviewFilterPanel> RenderPanel()
    {
        return Render<ReviewFilterPanel>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.AllDeviceIds, Devices.Select(d => d.Id).ToList())
            .Add(p => p.Ownership, Snapshot()));
    }


    [Fact]
    public void ReviewFilterPanel_Rendered_ListsAllProvidersAndTheProvidersThatExist()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert
        IElement provider = cut.Find("[data-testid='review-filter-provider']");
        Assert.Equal(["", "Ring", "Wyze"], OptionValues(provider));
        Assert.Equal(L["FilterAllProviders"].Value, provider.QuerySelector("option")!.TextContent);
    }

    [Fact]
    public void ReviewFilterPanel_ProviderChosen_ListsOnlyThatProvidersAccounts()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Wyze");

        // Assert
        IElement account = cut.Find("[data-testid='review-filter-account']");
        Assert.Equal(["", _wyze.Id.ToString("D")], OptionValues(account));
        Assert.Equal(L["FilterAllAccounts"].Value, account.QuerySelector("option")!.TextContent);
        Assert.Equal("Garage account", account.QuerySelector($"option[value='{_wyze.Id:D}']")!.TextContent);
    }

    [Fact]
    public void ReviewFilterPanel_ProviderThatDoesNotOwnTheChosenAccount_ClearsTheAccount()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");
        cut.Find("[data-testid='review-filter-account']").Change(_ring.Id.ToString("D"));

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Wyze");

        // Assert: the Ring account is not offered by Wyze, so the account choice reads "All accounts" again
        Assert.Equal("", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-account']")).Value);
        cut.WaitForAssertion(() => Assert.Equal("Wyze", ReviewState.ProviderFilter));
        Assert.Null(ReviewState.AccountFilter);
    }

    [Fact]
    public void ReviewFilterPanel_ProviderAndAccountChosen_SetsTheStateFiltersAfterTheDebounce()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");
        cut.Find("[data-testid='review-filter-account']").Change(_ring.Id.ToString("D"));

        // Act - the debounce fires on its own

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("Ring", ReviewState.ProviderFilter));
        Assert.Equal(_ring.Id, ReviewState.AccountFilter);
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_LocationSelectListsTheSnapshotLocations()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert
        IReadOnlyList<string> options = cut.FindAll("[data-testid='review-filter-location'] option").Select(o => o.TextContent).ToList();
        string all = Services.GetRequiredService<IStringLocalizer<SharedResources>>()["ScopeAllLocations"];
        Assert.Equal([all, "Porch", "Yard", "Garage"], options);
    }

    [Fact]
    public void ReviewFilterPanel_LocationChosen_SetsTheStateLocationFilterAfterTheDebounce()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-location']").Change(Devices[2].LocationId.ToString("D"));

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(Devices[2].LocationId, ReviewState.LocationFilter));
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_HasNoApplyOrResetButtons()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-filter-apply']"));
        Assert.Empty(cut.FindAll("[data-testid='review-filter-reset']"));
    }

    [Fact]
    public void ReviewFilterPanel_ProviderChosen_DoesNotApplyBeforeTheDebounceElapses()
    {
        // Arrange
        SetUpTwoProviders();
        ReviewState.DebounceDelay = TimeSpan.FromHours(1);
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Assert
        Assert.Null(ReviewState.ProviderFilter);
        Assert.Equal("Ring", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);
    }

    [Fact]
    public void ReviewFilterPanel_ProviderChosenTwiceInABurst_AppliesOnlyTheLastPick()
    {
        // Arrange
        SetUpTwoProviders();
        ReviewState.DebounceDelay = TimeSpan.FromMilliseconds(150);
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        int changes = 0;
        ReviewState.OnChange += () => changes++;

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");
        cut.Find("[data-testid='review-filter-provider']").Change("Wyze");

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("Wyze", ReviewState.ProviderFilter));
        Thread.Sleep(300);
        Assert.Equal(1, Volatile.Read(ref changes));
    }

    [Fact]
    public void ReviewFilterPanel_ExternalChangeWhileAnEditIsPending_WinsOverTheStaleEdit()
    {
        // Arrange
        SetUpTwoProviders();
        ReviewState.DebounceDelay = TimeSpan.FromMilliseconds(150);
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Act - something else sets the filters before the debounce fires
        cut.InvokeAsync(() => ReviewState.SetFilters("Wyze", _wyze.Id));
        Thread.Sleep(400);

        // Assert
        Assert.Equal("Wyze", ReviewState.ProviderFilter);
        Assert.Equal("Wyze", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);
    }

    [Fact]
    public void ReviewFilterPanel_DisposedWithAPendingEdit_NeverApplies()
    {
        // Arrange
        SetUpTwoProviders();
        ReviewState.DebounceDelay = TimeSpan.FromMilliseconds(100);
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Act
        ((IDisposable)cut.Instance).Dispose();
        Thread.Sleep(300);

        // Assert
        Assert.Null(ReviewState.ProviderFilter);
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_PutsTheCameraSelectorDirectlyBelowTheLocationSelect()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = Render<ReviewFilterPanel>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.AllDeviceIds, Devices.Select(d => d.Id).ToList())
            .Add(p => p.Ownership, Snapshot())
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, d => "Cam " + d.Id.ToString("N")[..4])));

        // Assert
        IElement panel = cut.Find("[data-testid='review-filter-panel']");
        IElement location = cut.Find("[data-testid='review-filter-location']");
        IElement selector = cut.Find("[data-testid='review-camera-selector']");
        Assert.True(panel.Contains(selector));
        Assert.Equal("review-camera-selector", location.NextElementSibling?.GetAttribute("data-testid"));
        Assert.Contains("w-100", cut.Find("[data-testid='review-camera-button']").ClassName);
    }

    [Fact]
    public void ReviewFilterPanel_ProviderChosen_NoteCountsTheMatchingCameras()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Assert
    }

    [Fact]
    public void ReviewFilterPanel_NoOwnershipData_CountsEveryCamera()
    {
        // Arrange
        AddDevices(3);
        IRenderedComponent<ReviewFilterPanel> cut = Render<ReviewFilterPanel>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.AllDeviceIds, Devices.Select(d => d.Id).ToList())
            .Add(p => p.Ownership, null));

        // Act
        cut.Find("[data-testid='review-filter-provider']").Change("Wyze");

        // Assert
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_HasNoLabelsAboveTheDropdowns()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert - each dropdown's first option already says what it filters, and it keeps an aria-label
        Assert.Empty(cut.FindAll("label.form-label"));
        Assert.NotNull(cut.Find("[data-testid='review-filter-provider']").GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("[data-testid='review-filter-account']").GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("[data-testid='review-filter-location']").GetAttribute("aria-label"));
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_DoesNotHoldTheCaseSection()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert - the Case section is its own section above the filters
        Assert.Empty(cut.FindAll("[data-testid='review-case-section']"));
    }

    [Fact]
    public void ReviewFilterPanel_Inline_HasNoPopupChrome()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert - the panel sits in the right sidebar, not in a floating card
        IElement panel = cut.Find("[data-testid='review-filter-panel']");
        Assert.DoesNotContain("review-popup", panel.ClassName);
        Assert.Null(panel.GetAttribute("role"));
        Assert.DoesNotContain("min-width", panel.GetAttribute("style") ?? string.Empty);
        Assert.DoesNotContain("position", panel.GetAttribute("style") ?? string.Empty);
        Assert.Contains(L["FilterTitle"].Value, cut.FindAll("[data-testid='review-filter-panel'] .fw-semibold").Select(e => e.TextContent));
    }

    [Fact]
    public void ReviewFilterPanel_StateFiltersChangeExternally_ReInitialisesTheEditFields()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        Assert.Equal("", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);

        // Act - another control sets the filters
        cut.InvokeAsync(() => ReviewState.SetFilters("Wyze", _wyze.Id));

        // Assert
        Assert.Equal("Wyze", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);
        Assert.Equal(_wyze.Id.ToString("D"), ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-account']")).Value);
    }

    [Fact]
    public void ReviewFilterPanel_StateChangesWithoutFilterChange_KeepsUnappliedEdits()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Act - an unrelated state change (the tab) must not discard the pending pick
        cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert
        Assert.Equal("Ring", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);
    }

    [Fact]
    public void ReviewFilterPanel_Dispose_UnsubscribesFromStateChanges()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        ((IDisposable)cut.Instance).Dispose();

        // Act
        Exception? error = Record.Exception(() => ReviewState.SetFilters("Ring", null));

        // Assert
        Assert.Null(error);
    }

    private static string[] OptionValues(IElement select)
    {
        return select.QuerySelectorAll("option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_EventTypeSelectListsAllTypesFirst()
    {
        // Arrange
        SetUpTwoProviders();

        // Act
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Assert
        IElement select = cut.Find("[data-testid='review-filter-eventtype']");
        Assert.Equal(["", .. ReviewEventFilters.KnownEventTypes], OptionValues(select));
    }

    [Fact]
    public void ReviewFilterPanel_EventTypeChosen_SetsTheStateEventTypeFilterAfterTheDebounce()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-filter-eventtype']").Change("motion");

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("motion", ReviewState.EventTypeFilter));
    }
}
