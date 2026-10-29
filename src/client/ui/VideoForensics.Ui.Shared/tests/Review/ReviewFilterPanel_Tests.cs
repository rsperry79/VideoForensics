using AngleSharp.Dom;

using AngleSharp.Html.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review filter panel: provider and account choices, the account reset on a provider change, Apply, Reset, and the matching-camera note.</summary>
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
            new(Devices[0].Id, _ring.Id, "Ring"),
            new(Devices[1].Id, _ring.Id, "Ring"),
            new(Devices[2].Id, _wyze.Id, "Wyze"),
        };
        var names = new Dictionary<Guid, string>
        {
            [_ring.Id] = "Front door account",
            [_wyze.Id] = "Garage account",
        };
        return new DeviceOwnershipSnapshot(Accounts.ToList(), names, ["Ring", "Wyze"], ownership);
    }

    private IRenderedComponent<ReviewFilterPanel> RenderPanel()
    {
        return Render<ReviewFilterPanel>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.AllDeviceIds, Devices.Select(d => d.Id).ToList())
            .Add(p => p.Ownership, Snapshot()));
    }

    private static string[] OptionValues(IElement select)
    {
        return select.QuerySelectorAll("option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
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
        cut.Find("[data-testid='review-filter-apply']").Click();
        Assert.Equal("Wyze", ReviewState.ProviderFilter);
        Assert.Null(ReviewState.AccountFilter);
    }

    [Fact]
    public void ReviewFilterPanel_ApplyWithProviderAndAccount_SetsTheStateFilters()
    {
        // Arrange
        SetUpTwoProviders();
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");
        cut.Find("[data-testid='review-filter-account']").Change(_ring.Id.ToString("D"));

        // Act
        cut.Find("[data-testid='review-filter-apply']").Click();

        // Assert
        Assert.Equal("Ring", ReviewState.ProviderFilter);
        Assert.Equal(_ring.Id, ReviewState.AccountFilter);
    }

    [Fact]
    public void ReviewFilterPanel_Apply_InvokesTheCloseCallback()
    {
        // Arrange
        SetUpTwoProviders();
        bool closed = false;
        IRenderedComponent<ReviewFilterPanel> cut = Render<ReviewFilterPanel>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.AllDeviceIds, Devices.Select(d => d.Id).ToList())
            .Add(p => p.Ownership, Snapshot())
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        // Act
        cut.Find("[data-testid='review-filter-apply']").Click();

        // Assert
        Assert.True(closed);
    }

    [Fact]
    public void ReviewFilterPanel_Reset_ReturnsTheChoicesToAllAndApplyClearsTheState()
    {
        // Arrange
        SetUpTwoProviders();
        ReviewState.SetFilters("Ring", _ring.Id);
        IRenderedComponent<ReviewFilterPanel> cut = RenderPanel();

        // Act
        cut.Find("[data-testid='review-filter-reset']").Click();

        // Assert: the choices are back to all, and the note counts every camera again
        Assert.Equal("", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-provider']")).Value);
        Assert.Equal("", ((IHtmlSelectElement)cut.Find("[data-testid='review-filter-account']")).Value);
        Assert.Equal(L["FilterMatchingCameras", 3].Value, cut.Find("[data-testid='review-filter-count']").TextContent.Trim());

        cut.Find("[data-testid='review-filter-apply']").Click();
        Assert.Null(ReviewState.ProviderFilter);
        Assert.Null(ReviewState.AccountFilter);
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
        Assert.Equal(L["FilterMatchingCameras", 2].Value, cut.Find("[data-testid='review-filter-count']").TextContent.Trim());
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
        Assert.Equal(L["FilterMatchingCameras", 3].Value, cut.Find("[data-testid='review-filter-count']").TextContent.Trim());
    }
}
