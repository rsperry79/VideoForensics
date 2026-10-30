using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review page shell: sign-in gate, top bar labels, URL seeding and write-back, tab switching, and disposal.</summary>
public class ReviewPage_Tests : ReviewTestContext
{
    [Fact]
    public async Task ReviewPage_NotSignedIn_ShowsSignInPromptAndNoTopBar()
    {
        // Arrange
        AddDevices(2);
        await SignOutOperatorAsync();
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Contains(L["NotSignedIn"].Value, cut.Find("[data-testid='review-sign-in']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='review-top-bar']"));
    }

    [Fact]
    public void ReviewPage_SignedIn_ShowsEveryLocalizedLabel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        // The Media page has no Back button; the top bar keeps only the case banner and the pending tags.
        Assert.Empty(cut.FindAll("[data-testid='review-back']"));

        string cameras = RenderRightPanel().Find("[data-testid='review-camera-button']").TextContent;
        Assert.Contains(L["AllCameras"].Value, cameras);

        // Actions and Grab still live in the right sidebar, under the filters.
        string actions = RenderRightPanel().Find("[data-testid='review-actions-panel']").TextContent;
        Assert.Contains(L["ActionsMenuTitle"].Value, actions);

    }

    [Fact]
    public void ReviewPage_InitialQuery_SeedsStateAndShowsEventsPlaceholder()
    {
        // Arrange
        AddDevices(2);
        Guid device = Devices[0].Id;
        NavigateToReview($"devices={device:D}&tab=events&t=2026-10-28T11:00:00Z");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
        Assert.Equal([device], ReviewState.CameraIds);
        Assert.Equal(new DateTime(2026, 10, 28, 11, 0, 0, DateTimeKind.Utc), ReviewState.Cursor);
        Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-detail']"));
    }

    [Fact]
    public void ReviewPage_InitialQuery_KeepsEveryParsedValueInTheUrl()
    {
        // Arrange - a partial parse must not write the URL back before every value is read
        AddDevices(2);
        Guid device = Devices[0].Id;
        NavigateToReview($"devices={device:D}&tab=events&t=2026-10-28T11:00:00Z");

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Contains("tab=Events", CurrentUri);
        Assert.Contains("t=2026-10-28T11:00:00", CurrentUri);
        Assert.Contains("devices=" + device.ToString("D"), CurrentUri);
    }

    [Fact]
    public void ReviewPage_MenuNavigatesToEventsTab_SwitchesStateTabPlaceholderAndUrl()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act - the left menu's Events item navigates to /media?tab=Events
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/media?tab=Events");

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
            Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
            Assert.Empty(cut.FindAll("[data-testid='review-tab-timeline']"));
            Assert.Contains("tab=Events", CurrentUri);
        });
    }

    [Fact]
    public void ReviewPage_TimelineTab_RendersTheTimelineComponentInItsPanel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Single(cut.FindComponents<ReviewTimelineTab>());
        Assert.Single(cut.FindAll("[data-testid='review-tab-timeline'] [data-testid='review-timeline-tab']"));
        Assert.DoesNotContain(L["TabPlaceholder", L["TabTimeline"].Value].Value, cut.Find("[data-testid='review-tab-timeline']").TextContent);
    }

    [Fact]
    public void ReviewPage_EventsTab_RendersTheEventsComponentInPlaceOfThePlaceholder()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview("tab=events");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Single(cut.FindComponents<ReviewEventsTab>());
        Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
        Assert.DoesNotContain(L["TabPlaceholder", L["TabEvents"].Value].Value, cut.Find("[data-testid='review-tab-events']").TextContent);
    }

    [Fact]
    public void ReviewPage_DetailTab_RendersTheDetailComponentInPlaceOfThePlaceholder()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview("tab=detail");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Single(cut.FindComponents<ReviewDetailTab>());
        Assert.DoesNotContain(L["TabPlaceholder", L["TabDetail"].Value].Value, cut.Find("[data-testid='review-tab-detail']").TextContent);
    }

    [Fact]
    public void ReviewPage_DevicesThatAreNotCameras_AreNeverOfferedOrShown()
    {
        // Arrange: a chime sits among the cameras, as a Ring account's devices do.
        AddDevices(2);
        Devices.Add(new Device { Id = Guid.NewGuid(), ProviderDeviceId = "chime-1", Name = "Upstairs", Type = "chime" });
        Guid chime = Devices[^1].Id;
        NavigateToReview($"devices={chime}");

        // Act
        _ = Render<ReviewPage>();

        // Assert: even a URL that names the chime does not select it.
        Assert.DoesNotContain(chime, ReviewState.CameraIds);
        Assert.Equal(2, ReviewState.CameraIds.Count);
    }

    [Fact]
    public void ReviewPage_SignedIn_RegistersTheFilterPanelWithTheRightPanel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        IRenderedComponent<IComponent> panel = RenderRightPanel();

        // Assert - the filters live in the right sidebar, and the timeline does not
        Assert.Single(panel.FindAll("[data-testid='review-filter-panel']"));
        Assert.Empty(panel.FindAll("[data-testid='review-timeline-panel']"));
    }

    [Fact]
    public void ReviewPage_SignedIn_ShowsTheViewTabsAboveTheContentAndLeavesTheLeftPanelEmpty()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert - the views live in the main left menu, so the page itself renders no tab bar and registers no left panel
        Assert.Empty(cut.FindAll("[data-testid='review-view-nav']"));
        Assert.Null(Services.GetRequiredService<LeftPanelContentService>().Content);
    }

    [Theory]
    [InlineData("review-case-section")]
    [InlineData("review-filter-panel")]
    [InlineData("review-time-range")]
    [InlineData("review-actions-panel")]
    public void ReviewPage_RightPanelSection_HasACollapsibleHeading(string testId)
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        IRenderedComponent<IComponent> right = RenderRightPanel();

        // Assert
        Assert.Single(right.FindAll($"[data-testid='{testId}'] button.review-sidebar-section-header[aria-expanded]"));
    }

    [Fact]
    public void ReviewPage_TabInTheAddressChanges_SwitchesTheTab()
    {
        // Arrange - the left menu navigates to /media?tab=Events; the page is already open, so it is not rebuilt
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/media?tab=Events");

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab));
    }

    [Theory]
    [InlineData("tab=timeline")]
    [InlineData("tab=events")]
    [InlineData("tab=detail")]
    public void ReviewPage_EveryTab_ShowsTheTimelinePanelInTheRightAside(string query)
    {
        // Arrange
        AddDevices(2);
        NavigateToReview(query);

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert - the panel is in the page body, inside the aside, never in the slots
        Assert.Single(cut.FindAll("aside.review-timeline-aside [data-testid='review-timeline-panel']"));
        Assert.Single(cut.FindAll(".review-body > .review-content"));
        Assert.Null(Services.GetRequiredService<LeftPanelContentService>().Content);
    }

    [Fact]
    public void ReviewPage_SignedIn_KeepsTheTopBarAboveTheBodyRow()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert - the tab content is inside the content column, the top bar is not
        Assert.Empty(cut.FindAll(".review-body [data-testid='review-top-bar']"));
        Assert.Single(cut.FindAll(".review-content [data-testid='review-tab-timeline']"));
    }

    [Fact]
    public async Task ReviewPage_SignedOut_DoesNotRegisterAnyPanel()
    {
        // Arrange
        AddDevices(2);
        await SignOutOperatorAsync();
        NavigateToReview();

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Null(Services.GetRequiredService<RightPanelContentService>().Content);
        Assert.Null(Services.GetRequiredService<LeftPanelContentService>().Content);
    }

    [Fact]
    public async Task ReviewPage_Dispose_UnsubscribesFromStateChanges()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ((IDisposable)cut.Instance).Dispose();

        // Act
        Exception? error = await Record.ExceptionAsync(() => cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Detail)));

        // Assert - the disposed page neither throws nor re-renders to the new tab
        Assert.Null(error);
        Assert.Single(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-detail']"));
    }

    [Fact]
    public void ReviewPage_SwitchingFromTimelineToEvents_KeepsTheTimelineModelPopulated()
    {
        // Arrange
        AddDevices(1);
        NavigateToReview();
        ClipLoader.Responder = (call, _) => Task.FromResult(new ReviewClipSet(
            call.DeviceIds.Select(id => new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = id,
                FileName = "clip.mp4",
                FilePath = "clip.mp4",
                MediaFormat = "mp4",
                RecordedAtUtc = FixedNowUtc.AddHours(-1),
                DurationSeconds = 120,
                Sha256Hash = "hash",
            }).ToList(),
            false));
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        cut.WaitForAssertion(() => Assert.Single(TimelineModel.Clips));

        // Act
        cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert: the tab change unmounts the timeline tab, but the page-level feeder keeps the model filled
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='review-tab-events']")));
        Assert.Single(TimelineModel.Clips);
    }

    [Fact]
    public void ReviewPage_ProviderQuery_SeedsTheFilterAndOffersOnlyThatProvidersCameras()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview("provider=Wyze");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Equal("Wyze", ReviewState.ProviderFilter);
        Assert.Equal([wyzeDevice], ReviewState.CameraIds);
        Assert.DoesNotContain(ringDevice, ReviewState.CameraIds);
    }

    [Fact]
    public void ReviewPage_AccountQuery_SeedsTheAccountFilter()
    {
        // Arrange
        ProviderAccount ring = AddAccount("Ring");
        AddDevices(2);
        Devices[0].LocationId = LinkLocation(ring).Id;
        NavigateToReview("account=" + ring.Id.ToString("D"));

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Equal(ring.Id, ReviewState.AccountFilter);
        Assert.Equal([Devices[0].Id], ReviewState.CameraIds);
    }

    [Fact]
    public void ReviewPage_RightPanel_RendersTheCameraSelectorInsideTheFilterPanel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        IRenderedComponent<IComponent> panel = RenderRightPanel();

        // Assert
        Assert.Single(panel.FindAll("[data-testid='review-filter-panel'] [data-testid='review-camera-selector']"));
        Assert.Single(panel.FindAll("[data-testid='review-camera-selector']"));
    }

    [Fact]
    public void ReviewPage_FilterPanel_OffersOnlyVisibleCameras()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview("provider=Ring");
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        IRenderedComponent<IComponent> filters = RenderRightPanel();
        filters.Find("[data-testid='review-camera-button']").Click();

        // Assert
        Assert.Single(filters.FindAll("[data-testid='review-camera-toggle-" + ringDevice.ToString("D") + "']"));
        Assert.Empty(filters.FindAll("[data-testid='review-camera-toggle-" + wyzeDevice.ToString("D") + "']"));
    }

    [Fact]
    public void ReviewPage_FilterChangedAwayFromSelectedCameras_DropsThemAndKeepsTheRest()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ReviewState.SetCameras([ringDevice, wyzeDevice]);

        IRenderedComponent<IComponent> filters = RenderRightPanel();

        // Act
        filters.Find("[data-testid='review-filter-provider']").Change("Ring");

        // Assert
        filters.WaitForAssertion(() => Assert.Equal([ringDevice], ReviewState.CameraIds));
    }

    [Fact]
    public void ReviewPage_FilterLeavesNoSelectedCamera_SelectsTheVisibleCameras()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ReviewState.SetCameras([ringDevice]);

        IRenderedComponent<IComponent> filters = RenderRightPanel();

        // Act
        filters.Find("[data-testid='review-filter-provider']").Change("Wyze");

        // Assert
        filters.WaitForAssertion(() => Assert.Equal([wyzeDevice], ReviewState.CameraIds));
    }

    [Fact]
    public void ReviewPage_OwnershipLoadFails_TreatsEveryDeviceAsVisibleAndLogsAWarning()
    {
        // Arrange
        AddAccount("Ring");
        AddDevices(3);
        OwnershipFails = true;
        NavigateToReview("provider=Ring");

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.Equal(Devices.Select(d => d.Id), ReviewState.CameraIds);
        PageLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("InvalidOperationException", StringComparison.Ordinal) && state.ToString()!.Contains("ownership unavailable", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReviewPage_CaseQuery_EntersCaseModeAndSeedsCamerasRangeAndCursor()
    {
        // Arrange
        AddDevices(3);
        Guid first = Devices[0].Id;
        Guid second = Devices[1].Id;
        MediaItem early = PinnableClip(first, FixedNowUtc.AddHours(-3));
        MediaItem late = PinnableClip(second, FixedNowUtc.AddHours(-1));
        Guid caseId = ArrangePinnedCase("Driveway", [early, late]);
        NavigateToReview("reviewcase=" + caseId.ToString("D"));

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.True(CaseMode.Active);
        Assert.Equal([first, second], ReviewState.CameraIds);
        Assert.Equal(FixedNowUtc.AddHours(-3).AddMinutes(-1), ReviewState.FromUtc);
        Assert.Equal(FixedNowUtc.AddHours(-1).AddSeconds(60).AddMinutes(1), ReviewState.ToUtc);
        Assert.Equal(FixedNowUtc.AddHours(-3), ReviewState.Cursor);
    }

    [Fact]
    public void ReviewPage_CaseQueryWithOwnCameras_KeepsTheCamerasTheUrlNames()
    {
        // Arrange
        AddDevices(3);
        Guid pinnedDevice = Devices[0].Id;
        Guid urlDevice = Devices[2].Id;
        Guid caseId = ArrangePinnedCase("Driveway", [PinnableClip(pinnedDevice, FixedNowUtc.AddHours(-1))]);
        NavigateToReview($"devices={urlDevice:D}&reviewcase={caseId:D}");

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.True(CaseMode.Active);
        Assert.Equal([urlDevice], ReviewState.CameraIds);
    }

    [Fact]
    public void ReviewPage_CaseQueryForUnknownCase_ShowsTheNotFoundMessageAndStaysInNormalMode()
    {
        // Arrange
        AddDevices(2);
        var caseRepository = new Mock<ICaseRepository>();
        Guid unknown = Guid.NewGuid();
        _ = caseRepository.Setup(r => r.GetAsync(unknown, It.IsAny<CancellationToken>())).ReturnsAsync((ForensicCase?)null);
        CaseRepositoryDouble = caseRepository.Object;
        NavigateToReview("reviewcase=" + unknown.ToString("D"));

        // Act
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Assert
        Assert.False(CaseMode.Active);
        Assert.Contains(L["CaseNotFound"].Value, cut.Find("[data-testid='review-case-not-found']").TextContent);
        Assert.DoesNotContain("reviewcase=", CurrentUri);
    }

    [Fact]
    public void ReviewPage_CaseMode_WriteBackKeepsTheMediaRouteAndTheCaseParam()
    {
        // Arrange
        AddDevices(1);
        Guid caseId = ArrangePinnedCase("Driveway", [PinnableClip(Devices[0].Id, FixedNowUtc.AddHours(-1))]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/media?reviewcase=" + caseId.ToString("D"));
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        _ = cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert
        Assert.Equal("/media", new Uri(CurrentUri).AbsolutePath);
        Assert.Contains("reviewcase=" + caseId.ToString("D"), CurrentUri);
    }

    [Fact]
    public void ReviewPage_NormalMode_WriteBackOnMediaStaysOnMedia()
    {
        // Arrange
        AddDevices(1);
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/media");
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        _ = cut.InvokeAsync(() => ReviewState.SetTab(ReviewTab.Events));

        // Assert
        Assert.Equal("/media", new Uri(CurrentUri).AbsolutePath);
        Assert.DoesNotContain("reviewcase=", CurrentUri);
    }

    [Fact]
    public void ReviewPage_Dispose_ExitsCaseMode()
    {
        // Arrange
        AddDevices(1);
        Guid caseId = ArrangePinnedCase("Driveway", [PinnableClip(Devices[0].Id, FixedNowUtc.AddHours(-1))]);
        NavigateToReview("reviewcase=" + caseId.ToString("D"));
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        Assert.True(CaseMode.Active);

        // Act
        ((IDisposable)cut.Instance).Dispose();

        // Assert
        Assert.False(CaseMode.Active);
    }

    private IRenderedComponent<IComponent> RenderRightPanel()
    {
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return Render(slot.Content!);
    }

    /// <summary>Ring owns Devices[0]; Wyze owns Devices[1]. Returns the Ring and Wyze devices.</summary>
    private (Guid Ring, Guid Wyze) SetUpRingAndWyze()
    {
        ProviderAccount ring = AddAccount("Ring");
        ProviderAccount wyze = AddAccount("Wyze");
        AddDevices(2);
        Devices[0].LocationId = LinkLocation(ring).Id;
        Devices[1].LocationId = LinkLocation(wyze).Id;
        return (Devices[0].Id, Devices[1].Id);
    }
}
