using AngleSharp.Dom;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review top bar: back button, range presets, popups, and case banner.</summary>
public class ReviewTopBar_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewTopBar> RenderBar()
    {
        AddDevices(2);
        return Render<ReviewTopBar>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf))
            .Add(p => p.UtcNow, () => FixedNowUtc));
    }

    [Fact]
    public async Task ReviewTopBar_CaseModeActive_ShowsTheBannerWithTheCaseName()
    {
        // Arrange
        AddDevices(1);
        Guid caseId = ArrangePinnedCase("Driveway", [PinnableClip(Devices[0].Id, FixedNowUtc.AddHours(-1))]);
        _ = await CaseMode.EnterAsync(caseId, CancellationToken.None);

        // Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        string banner = cut.Find("[data-testid='review-case-banner']").TextContent;
        Assert.Contains(L["CaseModeBanner", "Driveway"].Value, banner);
        Assert.Empty(cut.FindAll("[data-testid='review-case-truncated']"));
    }

    [Fact]
    public async Task ReviewTopBar_CaseModeTruncated_ShowsTheTruncationNotice()
    {
        // Arrange
        AddDevices(1);
        var clips = Enumerable.Range(0, ReviewCaseMode.MaxItemsPerKind + 1)
            .Select(i => PinnableClip(Devices[0].Id, FixedNowUtc.AddMinutes(-i)))
            .ToList();
        Guid caseId = ArrangePinnedCase("Big", clips);
        _ = await CaseMode.EnterAsync(caseId, CancellationToken.None);

        // Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        Assert.Contains(L["CaseModeTruncated", ReviewCaseMode.MaxItemsPerKind].Value, cut.Find("[data-testid='review-case-truncated']").TextContent);
    }

    [Fact]
    public void ReviewTopBar_CaseModeInactive_ShowsNoBanner()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-case-banner']"));
    }

    [Fact]
    public async Task ReviewTopBar_LeaveCaseClick_ExitsCaseModeAndNavigatesWithoutTheCaseParam()
    {
        // Arrange
        AddDevices(1);
        Guid caseId = ArrangePinnedCase("Driveway", [PinnableClip(Devices[0].Id, FixedNowUtc.AddHours(-1))]);
        _ = await CaseMode.EnterAsync(caseId, CancellationToken.None);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/media?reviewcase=" + caseId.ToString("D") + "&tab=Events");
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-case-leave']").Click();

        // Assert
        Assert.False(CaseMode.Active);
        Assert.DoesNotContain("reviewcase=", CurrentUri);
        Assert.Contains("tab=Events", CurrentUri);
        Assert.Equal("/media", new Uri(CurrentUri).AbsolutePath);
    }

    [Fact]
    public void ReviewTopBar_BackClick_GoesBackInBrowserHistory()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        string before = CurrentUri;

        // Act
        cut.Find("[data-testid='review-back']").Click();

        // Assert: /media is this page, so Back uses history rather than navigating to it.
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "history.back");
        Assert.Equal(before, CurrentUri);
    }

    [Fact]
    public void ReviewTopBar_Actions_IsEnabledAndOpensTheMenu()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        IElement actions = cut.Find("[data-testid='review-actions']");
        Assert.False(actions.HasAttribute("disabled"));

        // Act: the button opens the actions popup
        actions.Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-actions-popup']"));
    }

    [Fact]
    public void ReviewTopBar_RangePresetLastSevenDays_SetsStateRangeEndingNow()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-range']").Change("week");

        // Assert
        Assert.Equal(FixedNowUtc.AddDays(-7), ReviewState.FromUtc);
        Assert.Equal(FixedNowUtc, ReviewState.ToUtc);
    }

    [Fact]
    public void ReviewTopBar_PendingTagsExist_ShowsTheBadge()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        Assert.Empty(cut.FindAll("[data-testid='review-pending-tags-count']"));

        // Act
        cut.InvokeAsync(() => Services.GetRequiredService<PendingEvidenceTags>().Add(new PendingTag(
            CaseItemKind.Media, Guid.NewGuid(), Devices[0].Id, FixedNowUtc, PendingTagFakes.Reason, "actor-1")));

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-pending-tags-count']"));
    }

    [Fact]
    public void ReviewTopBar_Rendered_HasNoTabGroupLiveButtonOrFilterControls()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert - these moved to the left view nav and the right filter panel
        Assert.Empty(cut.FindAll("[data-testid='review-tabs']"));
        Assert.Empty(cut.FindAll("[data-testid^='review-tab-button-']"));
        Assert.Empty(cut.FindAll("[data-testid='review-live']"));
        Assert.Empty(cut.FindAll("[data-testid='review-filter']"));
        Assert.Empty(cut.FindAll("[data-testid='review-filter-active']"));
        Assert.Empty(cut.FindAll("[data-testid='review-filter-panel']"));
    }

    [Fact]
    public void ReviewTopBar_NoPopupOpen_RendersNoBackdrop()
    {
        // Arrange and Act
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-popup-backdrop']"));
    }

    [Fact]
    public void ReviewTopBar_PopupOpen_RendersABackdrop()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();

        // Act
        cut.Find("[data-testid='review-actions']").Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-popup-backdrop']"));
    }

    [Fact]
    public void ReviewTopBar_BackdropClicked_ClosesThePopup()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        cut.Find("[data-testid='review-actions']").Click();
        Assert.Single(cut.FindAll("[data-testid='review-actions-popup']"));

        // Act
        cut.Find("[data-testid='review-popup-backdrop']").Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-actions-popup']"));
        Assert.Empty(cut.FindAll("[data-testid='review-popup-backdrop']"));
    }

    [Fact]
    public void ReviewTopBar_EscapePressedOnTheTopBar_ClosesThePopup()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        cut.Find("[data-testid='review-actions']").Click();

        // Act
        cut.Find("[data-testid='review-top-bar']").KeyDown("Escape");

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-actions-popup']"));
    }

    [Fact]
    public void ReviewTopBar_OtherKeyPressedOnTheTopBar_KeepsThePopupOpen()
    {
        // Arrange
        IRenderedComponent<ReviewTopBar> cut = RenderBar();
        cut.Find("[data-testid='review-actions']").Click();

        // Act
        cut.Find("[data-testid='review-top-bar']").KeyDown("a");

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-actions-popup']"));
    }
}
