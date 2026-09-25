namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Device = VideoForensics.Data.Common.Entities.Device;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Evidence;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Shared scenario setup for Evidence page tests: one device, one event with a linked image
/// media item, and one media-only video item, both occurring on the same UTC day (the event is
/// newer). Repositories are Moq doubles that complete synchronously (ReturnsAsync from an
/// already-completed Task), which is what lets the page's initial load - including its
/// fire-and-forget viewer/thumbnail follow-up calls - finish before Render&lt;Evidence&gt;()
/// returns, the same assumption ScopeRailTests and ForensicGridTests rely on.
/// </summary>
public abstract class EvidencePageTestBase : BunitContext
{
    protected static readonly DateTime FixedNowUtc = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime EventOccurredAtUtc = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime VideoRecordedAtUtc = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

    protected readonly Guid DeviceId = Guid.NewGuid();
    protected readonly Guid EventId = Guid.NewGuid();
    protected readonly Guid ImageMediaId = Guid.NewGuid();
    protected readonly Guid VideoMediaId = Guid.NewGuid();

    protected Device Device { get; }
    protected Event EventEntity { get; }
    protected MediaItem ImageMedia { get; }
    protected MediaItem VideoMedia { get; }

    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();
    protected Mock<IEventRepository> EventRepositoryMock { get; } = new();
    protected Mock<IReportGenerationService> ReportGenerationServiceMock { get; } = new();
    protected Mock<ILegalHoldRepository> LegalHoldRepositoryMock { get; } = new();
    protected Mock<IMediaItemRepository> MediaItemRepositoryMock { get; } = new();
    protected Mock<IMediaContentUrlProvider> MediaUrlProviderMock { get; } = new();
    protected Mock<IDeviceHealthRepository> DeviceHealthRepositoryMock { get; } = new();

    protected ScopeState ScopeState { get; }
    protected InspectorState InspectorState { get; } = new();

    /// <summary>
    /// A real, minimal subclass of <see cref="Syncfusion.Blazor.Grids.SfGrid{TValue}"/> used as a
    /// bUnit component double for <c>SfGrid&lt;EvidenceItem&gt;</c>, the same trick
    /// <c>ForensicGridTests</c> uses: bUnit's generic stub isn't assignable to the typed
    /// <c>@ref</c> capture inside <c>ForensicGrid&lt;TItem&gt;</c>, but a real subclass is.
    /// </summary>
    private sealed class TestSfGrid : Syncfusion.Blazor.Grids.SfGrid<EvidenceItem>
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
        }

        protected override Task OnInitializedAsync() => Task.CompletedTask;

        protected override Task OnParametersSetAsync() => Task.CompletedTask;

        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    protected EvidencePageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var mediaViewerModule = JSInterop.SetupModule("./_content/VideoForensics.Ui.Shared/js/media-viewer.js");
        mediaViewerModule.SetupVoid("focusElement", _ => true);
        mediaViewerModule.SetupVoid("setPlaybackRate", _ => true);
        mediaViewerModule.SetupVoid("stepFrame", _ => true);
        mediaViewerModule.SetupVoid("togglePlay", _ => true);

        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        // Only exercised by Grid-view tests, but harmless to register unconditionally.
        ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<EvidenceItem>, TestSfGrid>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<EvidenceItem>>();

        Device = new Device
        {
            Id = DeviceId,
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = "provider-device-1",
            Name = "Front Door",
            Type = "camera"
        };

        EventEntity = new Event
        {
            Id = EventId,
            DeviceId = DeviceId,
            ProviderEventId = "evt-1",
            EventType = "Motion",
            OccurredAtUtc = EventOccurredAtUtc,
            DiscoveredAtUtc = EventOccurredAtUtc.AddSeconds(2),
            DownloadedAtUtc = EventOccurredAtUtc.AddSeconds(30)
        };

        ImageMedia = new MediaItem
        {
            Id = ImageMediaId,
            DeviceId = DeviceId,
            DownloadEventId = EventId,
            FileName = "motion.jpg",
            FilePath = "/media/motion.jpg",
            MediaFormat = "image/jpeg",
            FileSizeBytes = 12345,
            RecordedAtUtc = EventOccurredAtUtc,
            DownloadedAtUtc = EventOccurredAtUtc.AddSeconds(30),
            Sha256Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        };

        VideoMedia = new MediaItem
        {
            Id = VideoMediaId,
            DeviceId = DeviceId,
            DownloadEventId = null,
            FileName = "clip.mp4",
            FilePath = "/media/clip.mp4",
            MediaFormat = "video/mp4",
            FileSizeBytes = 999999,
            RecordedAtUtc = VideoRecordedAtUtc,
            DownloadedAtUtc = VideoRecordedAtUtc.AddMinutes(5),
            Sha256Hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        };

        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { Device });

        EventRepositoryMock
            .Setup(m => m.ListByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event> { EventEntity });

        ReportGenerationServiceMock
            .Setup(m => m.BuildEvidenceReviewAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport
            {
                GeneratedAtUtc = FixedNowUtc,
                ReportFromUtc = FixedNowUtc.AddDays(-7),
                ReportToUtc = FixedNowUtc,
                MediaItems = new List<MediaItem> { ImageMedia },
                IntegrityRecords = new List<IntegrityRecord>()
            });

        LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        MediaItemRepositoryMock
            .Setup(m => m.GetByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem> { ImageMedia, VideoMedia });

        MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                Task.FromResult<IReadOnlyDictionary<Guid, string>>(ids.ToDictionary(id => id, TicketedUrl)));

        DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceHealth>());

        var timeProvider = new FakeTimeProvider(FixedNowUtc);
        ScopeState = new ScopeState(timeProvider);

        Services.AddScoped(_ => AuthServiceMock.Object);
        Services.AddScoped(_ => DeviceRepositoryMock.Object);
        Services.AddScoped(_ => EventRepositoryMock.Object);
        Services.AddScoped(_ => ReportGenerationServiceMock.Object);
        Services.AddScoped(_ => LegalHoldRepositoryMock.Object);
        Services.AddScoped(_ => MediaItemRepositoryMock.Object);
        Services.AddScoped(_ => MediaUrlProviderMock.Object);
        Services.AddScoped(_ => DeviceHealthRepositoryMock.Object);
        Services.AddScoped(_ => ScopeState);
        Services.AddScoped(_ => InspectorState);
        Services.AddScoped<RightPanelContentService>();
    }

    /// <summary>Deterministic stand-in for the provider's short-lived, per-media access ticket.</summary>
    protected static string TicketedUrl(Guid mediaId) =>
        $"https://cdn.example.test/media/{mediaId:N}?ticket=tkt-{mediaId:N}";

    protected IRenderedComponent<Evidence> RenderEvidencePage() => Render<Evidence>();
}

public class Evidence_Timeline_Tests : EvidencePageTestBase
{
    [Fact]
    public void DefaultView_RendersBothItems_UnderCorrectDayHeading_EventFirst()
    {
        var component = RenderEvidencePage();

        var dayHeading = component.Find(".timeline-day-header");
        var expectedDay = DateOnly.FromDateTime(EventOccurredAtUtc).ToString("yyyy-MM-dd (ddd)");
        Assert.Contains(expectedDay, dayHeading.TextContent);
        Assert.Contains("2 items", dayHeading.TextContent);

        var rows = component.FindAll(".timeline-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal($"event:{EventId}", rows[0].GetAttribute("data-key"));
        Assert.Equal($"media:{VideoMediaId}", rows[1].GetAttribute("data-key"));
    }
}

public class Evidence_Gallery_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickGalleryButton_SwitchesToGalleryView_RendersTwoTiles_WithTicketedImageUrl()
    {
        var component = RenderEvidencePage();

        component.Find("[data-testid='view-gallery']").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=gallery", nav.Uri);

        var tiles = component.FindAll(".gallery-tile");
        Assert.Equal(2, tiles.Count);

        var img = component.Find($".gallery-tile[data-key='event:{EventId}'] img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));
    }
}

public class Evidence_Viewer_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickImageTile_OpensViewer_WithTicketedUrl_InspectorTitle_AndUrlItemParam()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();

        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));

        var img = component.Find(".viewer-media img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));

        var expectedTitle = $"{EventEntity.EventType} · {EventOccurredAtUtc:u}";
        Assert.Equal(expectedTitle, InspectorState.Current?.Title);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=event:{EventId}", nav.Uri);
    }

    [Fact]
    public void NextButton_MovesToVideoItem_FetchesFreshTicket_UpdatesInspectorAndUrl()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        MediaUrlProviderMock.Invocations.Clear();

        component.Find("[data-testid='viewer-next']").Click();

        MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(VideoMediaId)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var video = component.Find(".viewer-media video");
        Assert.Equal(TicketedUrl(VideoMediaId), video.GetAttribute("src"));

        Assert.Contains("Video", InspectorState.Current?.Title);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=media:{VideoMediaId}", nav.Uri);
    }

    [Fact]
    public void CloseButton_ClosesViewer_ReturnsToGallery_RemovesItemParam_KeepsView()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        component.Find("[data-testid='viewer-close']").Click();

        Assert.Empty(component.FindAll("[data-testid='media-viewer']"));
        Assert.NotEmpty(component.FindAll(".evidence-gallery"));
        Assert.Null(InspectorState.Current);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("item=", nav.Uri);
        Assert.Contains("view=gallery", nav.Uri);
    }
}

public class Evidence_DeepLink_Tests : EvidencePageTestBase
{
    [Fact]
    public void GridViewQueryParam_RendersGridView_TimelineAbsent()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/evidence?view=grid", replace: true);

        var component = RenderEvidencePage();

        Assert.NotNull(component.FindComponent<VideoForensics.Ui.Shared.Components.Inspector.ForensicGrid<EvidenceItem>>());
        Assert.Empty(component.FindAll(".evidence-timeline"));
        Assert.Empty(component.FindAll(".evidence-gallery"));
    }

    [Fact]
    public void ItemQueryParam_OpensViewerDirectly()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"/evidence?item=event:{EventId}", replace: true);

        var component = RenderEvidencePage();

        Assert.Single(component.FindAll("[data-testid='media-viewer']"));
        var img = component.Find(".viewer-media img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));
    }
}

public class Evidence_ErrorHandling_Tests : EvidencePageTestBase
{
    [Fact]
    public void MediaRepositoryThrows_ShowsDeviceNamedError_EventStillRendered()
    {
        MediaItemRepositoryMock
            .Setup(m => m.GetByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("media store unreachable"));

        var component = RenderEvidencePage();

        var markup = component.Markup;
        Assert.Contains("Front Door", markup);
        Assert.Contains("media store unreachable", markup);

        var rows = component.FindAll(".timeline-row");
        Assert.Single(rows);
        Assert.Equal($"event:{EventId}", rows[0].GetAttribute("data-key"));
    }
}

public class Evidence_ScopeChange_Tests : EvidencePageTestBase
{
    [Fact]
    public async Task ScopeChange_ReloadsRepositories_WithNewFromUtc()
    {
        var component = RenderEvidencePage();

        var newFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newScope = new ForensicScope(Array.Empty<Guid>(), newFrom, FixedNowUtc, null);

        await component.InvokeAsync(() => ScopeState.Set(newScope));

        MediaItemRepositoryMock.Verify(
            m => m.GetByDeviceAndDateRangeAsync(DeviceId, newFrom, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        EventRepositoryMock.Verify(
            m => m.ListByDeviceAndDateRangeAsync(DeviceId, newFrom, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

public class Evidence_Thumbnail_Tests : EvidencePageTestBase
{
    [Fact]
    public void ThumbnailProviderThrows_ItemsStillRender_NoThumbnails_MutedNoteShown()
    {
        MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("thumbnail service down"));

        var component = RenderEvidencePage();

        var rows = component.FindAll(".timeline-row");
        Assert.Equal(2, rows.Count);
        Assert.Empty(component.FindAll(".timeline-row-thumb"));

        Assert.NotEmpty(component.FindAll("[data-testid='thumbnails-unavailable']"));
    }
}

public class Evidence_ViewerErrorHandling_Tests : EvidencePageTestBase
{
    [Fact]
    public void ProviderThrows_WhenOpeningItem_ViewerShowsUnavailable_PageDoesNotCrash()
    {
        MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ticket service down"));

        var component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();

        // The click (and the async chain it drives, up through LoadMediaUrlAsync's now-awaited
        // failure) must complete without throwing out of the test - that is "doesn't crash".
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));

        var unavailable = component.Find(".viewer-unavailable");
        Assert.Contains("Media preview unavailable", unavailable.TextContent);
        Assert.Empty(component.FindAll(".viewer-media img"));

        var errorNote = component.Find("[data-testid='media-load-error']");
        Assert.Contains("ticket service down", errorNote.TextContent);
    }

    [Fact]
    public void OutOfOrderTicketCompletion_StaleFirstItemResponseDoesNotClobberSecondItem()
    {
        // The initial page load's thumbnail fetch and the later "open the image item" fetch
        // request the exact same single-element id set ([ImageMediaId]), so a plain argument
        // matcher can't tell them apart - SetupSequence lets the first (thumbnail) call resolve
        // immediately while the second (opening the item) call is held open on a TCS.
        var tcsImage = new TaskCompletionSource<IReadOnlyDictionary<Guid, string>>();
        var tcsVideo = new TaskCompletionSource<IReadOnlyDictionary<Guid, string>>();

        MediaUrlProviderMock
            .SetupSequence(m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(ImageMediaId)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                new Dictionary<Guid, string> { [ImageMediaId] = TicketedUrl(ImageMediaId) }))
            .Returns(tcsImage.Task);

        MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(VideoMediaId)),
                It.IsAny<CancellationToken>()))
            .Returns(tcsVideo.Task);

        var component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();

        // Open the image item - its ticket fetch (tcsImage) is now pending, unresolved.
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();
        Assert.Empty(component.FindAll(".viewer-media img"));

        // Move to the video item before the image's fetch has resolved; its own fetch
        // (tcsVideo) is now pending too.
        component.Find("[data-testid='viewer-next']").Click();

        // The second (video) item's ticket arrives first...
        tcsVideo.SetResult(new Dictionary<Guid, string> { [VideoMediaId] = TicketedUrl(VideoMediaId) });

        // ...then the stale first (image) item's ticket arrives late, after the user has
        // already moved on. It must be ignored rather than overwriting the video's URL.
        // Completing a TCS only posts the continuation back to the renderer's dispatcher; it
        // doesn't run inline, so WaitForAssertion polls until that posted work has actually
        // been processed and the render reflects it.
        tcsImage.SetResult(new Dictionary<Guid, string> { [ImageMediaId] = TicketedUrl(ImageMediaId) });

        component.WaitForAssertion(() => Assert.NotEmpty(component.FindAll(".viewer-media video")));

        var video = component.Find(".viewer-media video");
        Assert.Equal(TicketedUrl(VideoMediaId), video.GetAttribute("src"));

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=media:{VideoMediaId}", nav.Uri);
    }
}

public class Evidence_DeviceTime_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickDeviceTimeButton_SwitchesToDeviceTimeView_RendersGrid()
    {
        var component = RenderEvidencePage();

        component.Find("[data-testid='view-devicetime']").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=devicetime", nav.Uri);
        Assert.NotEmpty(component.FindAll("[data-testid='device-time-grid']"));
        Assert.Empty(component.FindAll(".evidence-timeline"));
    }

    [Fact]
    public void ClickCell_NarrowsScopeToDeviceAndBucket_AndSwitchesToTimeline()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();

        var cell = component.Find($"tr[data-key='{DeviceId}'] td");
        cell.Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=timeline", nav.Uri);
        Assert.Single(ScopeState.Current.DeviceIds);
        Assert.Equal(DeviceId, ScopeState.Current.DeviceIds[0]);
    }

    [Fact]
    public void ClickDeviceRowHeader_ShowsSnapshotStrip_ForThatDevicesViewableItems()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();

        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='snapshot-strip']"));
        var thumbs = component.FindAll("[data-testid='snapshot-strip-thumb']");
        Assert.Equal(2, thumbs.Count); // ImageMedia (via event) + VideoMedia are both viewable
    }

    [Fact]
    public void ClickDeviceRowHeader_FetchesRssiHistory_ForScopeRange()
    {
        DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceHealth>
            {
                new() { Id = Guid.NewGuid(), DeviceId = DeviceId, WifiSignalRssi = -60, CapturedAtUtc = EventOccurredAtUtc }
            });

        var component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();
        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        DeviceHealthRepositoryMock.Verify(
            m => m.GetHistoryAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        var polyline = component.Find("[data-testid='snapshot-strip-rssi'] polyline");
        Assert.False(string.IsNullOrWhiteSpace(polyline.GetAttribute("points")));
    }

    [Fact]
    public void ClickSnapshotStripThumbnail_OpensMediaViewer()
    {
        var component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();
        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        component.Find($"[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));
    }
}
