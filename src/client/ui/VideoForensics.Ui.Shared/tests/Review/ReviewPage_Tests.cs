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
        string topBar = cut.Find("[data-testid='review-top-bar']").TextContent;
        foreach (string key in new[]
        {
            "Back", "Live", "AllCameras", "RangeLast24Hours", "Filter", "Actions", "TabTimeline", "TabEvents", "TabDetail",
        })
        {
            Assert.Contains(L[key].Value, topBar);
        }
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
    public void ReviewPage_TabClick_SwitchesStateTabPlaceholderAndUrl()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        cut.Find("[data-testid='review-tab-button-events']").Click();

        // Assert
        Assert.Equal(ReviewTab.Events, ReviewState.ActiveTab);
        Assert.Single(cut.FindAll("[data-testid='review-tab-events']"));
        Assert.Empty(cut.FindAll("[data-testid='review-tab-timeline']"));
        Assert.Contains("tab=Events", CurrentUri);
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
    public void ReviewPage_SignedIn_RegistersTheTimelinePanelWithTheRightPanel()
    {
        // Arrange
        AddDevices(2);
        NavigateToReview();
        _ = Render<ReviewPage>();

        // Act
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        var panel = Render(slot.Content!);

        // Assert
        Assert.Single(panel.FindAll("[data-testid='review-timeline-panel']"));
    }

    [Fact]
    public async Task ReviewPage_SignedOut_DoesNotRegisterTheTimelinePanel()
    {
        // Arrange
        AddDevices(2);
        await SignOutOperatorAsync();
        NavigateToReview();

        // Act
        _ = Render<ReviewPage>();

        // Assert
        Assert.Null(Services.GetRequiredService<RightPanelContentService>().Content);
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
        Assert.Single(cut.FindAll("[data-testid='review-camera-button']"));
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
    public void ReviewPage_CameraSelectorOpened_OffersOnlyVisibleCameras()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview("provider=Ring");
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();

        // Act
        cut.Find("[data-testid='review-camera-button']").Click();

        // Assert
        Assert.Single(cut.FindAll("[data-testid='review-camera-toggle-" + ringDevice.ToString("D") + "']"));
        Assert.Empty(cut.FindAll("[data-testid='review-camera-toggle-" + wyzeDevice.ToString("D") + "']"));
    }

    [Fact]
    public void ReviewPage_FilterChangedAwayFromSelectedCameras_DropsThemAndKeepsTheRest()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ReviewState.SetCameras([ringDevice, wyzeDevice]);

        // Act
        cut.Find("[data-testid='review-filter']").Click();
        cut.Find("[data-testid='review-filter-provider']").Change("Ring");
        cut.Find("[data-testid='review-filter-apply']").Click();

        // Assert
        Assert.Equal([ringDevice], ReviewState.CameraIds);
    }

    [Fact]
    public void ReviewPage_FilterLeavesNoSelectedCamera_SelectsTheVisibleCameras()
    {
        // Arrange
        (Guid ringDevice, Guid wyzeDevice) = SetUpRingAndWyze();
        NavigateToReview();
        IRenderedComponent<ReviewPage> cut = Render<ReviewPage>();
        ReviewState.SetCameras([ringDevice]);

        // Act
        cut.Find("[data-testid='review-filter']").Click();
        cut.Find("[data-testid='review-filter-provider']").Change("Wyze");
        cut.Find("[data-testid='review-filter-apply']").Click();

        // Assert
        Assert.Equal([wyzeDevice], ReviewState.CameraIds);
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
        Assert.Single(cut.FindAll("[data-testid='review-camera-button']"));
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
        cut.Find("[data-testid='review-tab-button-events']").Click();

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
        cut.Find("[data-testid='review-tab-button-events']").Click();

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
