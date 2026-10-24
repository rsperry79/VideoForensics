
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Navigations;

using System.Reactive.Subjects;

using VideoForensics.Utils.Testing;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Client.Ui.Shared.Components.Evidence;
using VideoForensics.Client.Ui.Shared.Components.Inspector;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Evidence;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;
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
    /// <summary>Thumbnail-ready events the page under test receives; tests push through this subject.</summary>
    protected readonly System.Reactive.Subjects.Subject<ThumbnailReadyDto> ThumbnailReadySubject = new();

    /// <summary>Fake source that forwards <see cref="ThumbnailReadySubject"/> to the page.</summary>
    protected sealed class FakeThumbnailReadySource : IThumbnailReadySource
    {
        private readonly System.Reactive.Subjects.Subject<ThumbnailReadyDto> _ready;

        public FakeThumbnailReadySource(System.Reactive.Subjects.Subject<ThumbnailReadyDto> ready)
        {
            _ready = ready;
        }

        public IObservable<ThumbnailReadyDto> ThumbnailReady => _ready;
    }

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
    /// <summary>Evidence changes the test pushes; the page sees them through IEvidenceChangedSource.</summary>
    protected Subject<EvidenceChangedDto> EvidenceChanges { get; } = new();
    protected Mock<IDeviceHealthRepository> DeviceHealthRepositoryMock { get; } = new();
    protected Mock<IEvidenceValidationService> EvidenceValidationServiceMock { get; } = new();
    protected Mock<IMediaStillCaptureService> MediaStillCaptureServiceMock { get; } = new();
    protected Mock<ICaseRepository> CaseRepositoryMock { get; } = new();

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

        protected override Task OnInitializedAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnParametersSetAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            return Task.CompletedTask;
        }

        // The real SfGrid<T>.ShouldRender() touches internal Syncfusion state this double never
        // initializes (we skip its real lifecycle above), and throws once a sibling's state change
        // (e.g. the evidence-actions reason textbox) triggers a second SetParametersAsync pass on
        // this component. Only exercised once actions were added to the Grid view.
        protected override bool ShouldRender()
        {
            return true;
        }
    }

    protected EvidencePageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        BunitJSModuleInterop mediaViewerModule = JSInterop.SetupModule("./_content/VideoForensics.Ui.Shared/js/media-viewer.js");
        _ = mediaViewerModule.SetupVoid("focusElement", _ => true);
        _ = mediaViewerModule.SetupVoid("setPlaybackRate", _ => true);
        _ = mediaViewerModule.SetupVoid("stepFrame", _ => true);
        _ = mediaViewerModule.SetupVoid("togglePlay", _ => true);
        _ = mediaViewerModule.Setup<double>("getCurrentTimeMs", _ => true).SetResult(2500.0);

        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);

        // Only exercised by Grid-view tests, but harmless to register unconditionally.
        _ = ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<EvidenceItem>, TestSfGrid>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<EvidenceItem>>();

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

        _ = DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Device]);

        _ = EventRepositoryMock
            .Setup(m => m.ListByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([EventEntity]);

        _ = ReportGenerationServiceMock
            .Setup(m => m.BuildEvidenceReviewAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport
            {
                GeneratedAtUtc = FixedNowUtc,
                ReportFromUtc = FixedNowUtc.AddDays(-7),
                ReportToUtc = FixedNowUtc,
                MediaItems = [ImageMedia],
                // The page joins media to events by ProviderEventId, so the image must be linked to the event's id.
                MediaProviderEventLinks = [new(ImageMediaId, EventEntity.ProviderEventId)],
                IntegrityRecords = []
            });

        _ = LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _ = MediaItemRepositoryMock
            .Setup(m => m.GetByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ImageMedia, VideoMedia]);

        _ = MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                Task.FromResult<IReadOnlyDictionary<Guid, string>>(ids.ToDictionary(id => id, TicketedUrl)));

        _ = DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var timeProvider = new FakeTimeProvider(FixedNowUtc);
        ScopeState = new ScopeState(timeProvider);

        _ = Services.AddSingleton<IThumbnailReadySource>(new FakeThumbnailReadySource(ThumbnailReadySubject));
        _ = Services.AddScoped(_ => AuthServiceMock.Object);
        _ = Services.AddScoped(_ => DeviceRepositoryMock.Object);
        _ = Services.AddScoped(_ => EventRepositoryMock.Object);
        _ = Services.AddScoped(_ => ReportGenerationServiceMock.Object);
        _ = Services.AddScoped(_ => LegalHoldRepositoryMock.Object);
        _ = Services.AddScoped(_ => MediaItemRepositoryMock.Object);
        _ = Services.AddScoped(_ => MediaUrlProviderMock.Object);
        _ = Services.AddSingleton<IEvidenceChangedSource>(new EvidenceChangedSourceDouble(EvidenceChanges));
        _ = Services.AddScoped(_ => DeviceHealthRepositoryMock.Object);
        _ = Services.AddScoped(_ => EvidenceValidationServiceMock.Object);
        _ = Services.AddScoped(_ => MediaStillCaptureServiceMock.Object);
        _ = Services.AddScoped(_ => CaseRepositoryMock.Object);
        _ = Services.AddScoped(_ => ScopeState);
        _ = Services.AddScoped(_ => InspectorState);
        _ = Services.AddScoped<RightPanelContentService>();

        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        // Default: an operator is signed in (token, OperatorId, Review role) so the page renders its
        // content. Tests exercising the signed-out gate call SignOutOperatorAsync(). SetAsync only
        // awaits a loose-mode JSInterop call that completes immediately, so blocking on it is safe.
        Services.GetRequiredService<PairedSessionState>()
            .SetAsync("test-token", Guid.NewGuid(), "Review").GetAwaiter().GetResult();

        // SfToast (added for the evidence-actions toasts) checks RendererInfo during render; must
        // be set after all service registrations above, since setting it locks the provider.
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    /// <summary>Deterministic stand-in for the provider's short-lived, per-media access ticket.</summary>
    protected static string TicketedUrl(Guid mediaId)
    {
        return $"https://cdn.example.test/media/{mediaId:N}?ticket=tkt-{mediaId:N}";
    }

    /// <summary>
    /// Signs in an operator for this test. Mutates the PairedSessionState already registered in
    /// the constructor (resolving it caches the single scoped instance bUnit will later inject
    /// into the rendered component) rather than re-registering, since SetRendererInfo() above
    /// locks the service provider against further registration.
    /// </summary>
    /// <param name="role">The operator's role (default "Review", the minimum most evidence actions require).</param>
    protected async Task<Guid> SignInOperatorAsync(string role = "Review")
    {
        var operatorId = Guid.NewGuid();
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        await session.SetAsync("test-token", operatorId, role);
        return operatorId;
    }

    /// <summary>Clears the operator session, so the page shows its signed-out sign-in prompt.</summary>
    protected Task SignOutOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().ClearAsync();
    }

    protected IRenderedComponent<Evidence> RenderEvidencePage()
    {
        return Render<Evidence>();
    }
}

public class Evidence_Timeline_Tests : EvidencePageTestBase
{
    [Fact]
    public void DefaultView_RendersBothItems_UnderCorrectDayHeading_EventFirst()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        AngleSharp.Dom.IElement dayHeading = component.Find(".timeline-day-header");
        string expectedDay = DateOnly.FromDateTime(EventOccurredAtUtc).ToString("yyyy-MM-dd (ddd)");
        Assert.Contains(expectedDay, dayHeading.TextContent);
        Assert.Contains("· 2 items", dayHeading.TextContent);

        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll(".timeline-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal($"event:{EventId}", rows[0].GetAttribute("data-key"));
        Assert.Equal($"media:{VideoMediaId}", rows[1].GetAttribute("data-key"));
    }

    [Fact]
    public void DefaultView_RowsShowThumbnails_ForImageAndVideoMedia()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        AngleSharp.Dom.IElement imageRowThumb = component.Find($".timeline-row[data-key='event:{EventId}'] img");
        Assert.Equal(TicketedUrl(ImageMediaId), imageRowThumb.GetAttribute("src"));
        _ = Assert.Single(component.FindAll($".timeline-row[data-key='media:{VideoMediaId}'] img"));
    }
}

public class Evidence_Gallery_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickGalleryButton_SwitchesToGalleryView_RendersTwoTiles_WithTicketedImageUrl()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        component.Find("[data-testid='view-gallery']").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=gallery", nav.Uri);

        IReadOnlyList<AngleSharp.Dom.IElement> tiles = component.FindAll(".gallery-tile");
        Assert.Equal(2, tiles.Count);

        AngleSharp.Dom.IElement img = component.Find($".gallery-tile[data-key='event:{EventId}'] img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));
    }
}

/// <summary>
/// A thumbnail produced while the page is open re-fetches that item's URL, so its image reloads with the new still.
/// An event for an item the page does not show must not cause a fetch.
/// </summary>
public class Evidence_ThumbnailReady_Tests : EvidencePageTestBase
{
    [Fact]
    public void ThumbnailReady_ForItemOnPage_RefetchesThatItemsUrl()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        ThumbnailReadySubject.OnNext(new ThumbnailReadyDto(VideoMediaId));

        // The initial load fetches several items at once; the refresh fetches exactly this one.
        component.WaitForAssertion(() => MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(VideoMediaId)),
                It.IsAny<CancellationToken>()),
            Times.Once));
    }

    [Fact]
    public async Task ThumbnailReady_ForItemNotOnPage_DoesNotFetch()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        var unknown = Guid.NewGuid();

        ThumbnailReadySubject.OnNext(new ThumbnailReadyDto(unknown));
        await component.InvokeAsync(() => Task.CompletedTask);

        MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(unknown)), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ThumbnailReady_AfterDispose_DoesNotFetchOrThrow()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.WaitForAssertion(() => MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce));
        MediaUrlProviderMock.Invocations.Clear();

        ((IDisposable)component.Instance).Dispose();
        Exception? ex = Record.Exception(() => ThumbnailReadySubject.OnNext(new ThumbnailReadyDto(VideoMediaId)));
        await Task.Delay(50);

        Assert.Null(ex);
        MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(VideoMediaId)), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class Evidence_Viewer_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickImageTile_OpensViewer_WithTicketedUrl_InspectorTitle_AndUrlItemParam()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();

        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));

        AngleSharp.Dom.IElement img = component.Find(".viewer-media img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));

        string expectedTitle = $"{EventEntity.EventType} · {EventOccurredAtUtc:u}";
        Assert.Equal(expectedTitle, InspectorState.Current?.Title);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=event:{EventId}", nav.Uri);
    }

    [Fact]
    public void NextButton_MovesToVideoItem_FetchesFreshTicket_UpdatesInspectorAndUrl()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        MediaUrlProviderMock.Invocations.Clear();

        component.Find("[data-testid='viewer-next']").Click();

        MediaUrlProviderMock.Verify(
            m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(VideoMediaId)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        AngleSharp.Dom.IElement video = component.Find(".viewer-media video");
        Assert.Equal(TicketedUrl(VideoMediaId), video.GetAttribute("src"));

        Assert.Contains("Video", InspectorState.Current?.Title);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=media:{VideoMediaId}", nav.Uri);
    }

    [Fact]
    public void CloseButton_ClosesViewer_ReturnsToGallery_RemovesItemParam_KeepsView()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        component.Find("[data-testid='viewer-close']").Click();

        Assert.Empty(component.FindAll("[data-testid='media-viewer']"));
        Assert.NotEmpty(component.FindAll(".evidence-gallery"));
        Assert.Null(InspectorState.Current);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("item=", nav.Uri);
        Assert.Contains("view=gallery", nav.Uri);
    }
}

public class Evidence_DeepLink_Tests : EvidencePageTestBase
{
    [Fact]
    public void GridViewQueryParam_RendersGridView_TimelineAbsent()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/media?view=grid", replace: true);

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        Assert.NotNull(component.FindComponent<VideoForensics.Client.Ui.Shared.Components.Inspector.ForensicGrid<EvidenceItem>>());
        Assert.Empty(component.FindAll(".evidence-timeline"));
        Assert.Empty(component.FindAll(".evidence-gallery"));
    }

    [Fact]
    public void ItemQueryParam_OpensViewerDirectly()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"/media?item=event:{EventId}", replace: true);

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        _ = Assert.Single(component.FindAll("[data-testid='media-viewer']"));
        AngleSharp.Dom.IElement img = component.Find(".viewer-media img");
        Assert.Equal(TicketedUrl(ImageMediaId), img.GetAttribute("src"));
    }
}

public class Evidence_ErrorHandling_Tests : EvidencePageTestBase
{
    [Fact]
    public void MediaRepositoryThrows_ShowsDeviceNamedError_EventStillRendered()
    {
        _ = MediaItemRepositoryMock
            .Setup(m => m.GetByDeviceAndDateRangeAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("media store unreachable"));

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        string markup = component.Markup;
        Assert.Contains("Front Door", markup);
        Assert.Contains("media store unreachable", markup);

        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll(".timeline-row");
        _ = Assert.Single(rows);
        Assert.Equal($"event:{EventId}", rows[0].GetAttribute("data-key"));
    }
}

public class Evidence_ScopeChange_Tests : EvidencePageTestBase
{
    [Fact]
    public async Task ScopeChange_ReloadsRepositories_WithNewFromUtc()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

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
        _ = MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("thumbnail service down"));

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll(".timeline-row");
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
        _ = MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ticket service down"));

        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();

        // The click (and the async chain it drives, up through LoadMediaUrlAsync's now-awaited
        // failure) must complete without throwing out of the test - that is "doesn't crash".
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));

        AngleSharp.Dom.IElement unavailable = component.Find(".viewer-unavailable");
        Assert.Contains("Media preview unavailable", unavailable.TextContent);
        Assert.Empty(component.FindAll(".viewer-media img"));

        AngleSharp.Dom.IElement errorNote = component.Find("[data-testid='media-load-error']");
        Assert.Contains("ticket service down", errorNote.TextContent);
    }

    [Fact]
    public void OutOfOrderTicketCompletion_StaleFirstItemResponseDoesNotClobberSecondItem()
    {
        // The page loads thumbnails in one batched call, so the only single-id fetch for the image is the viewer's
        // ticket request when the image is opened. Hold that fetch open on a TCS.
        var tcsImage = new TaskCompletionSource<IReadOnlyDictionary<Guid, string>>();
        var tcsVideo = new TaskCompletionSource<IReadOnlyDictionary<Guid, string>>();

        _ = MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(ImageMediaId)),
                It.IsAny<CancellationToken>()))
            .Returns(tcsImage.Task);

        _ = MediaUrlProviderMock
            .Setup(m => m.GetContentUrlsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(VideoMediaId)),
                It.IsAny<CancellationToken>()))
            .Returns(tcsVideo.Task);

        IRenderedComponent<Evidence> component = RenderEvidencePage();
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

        AngleSharp.Dom.IElement video = component.Find(".viewer-media video");
        Assert.Equal(TicketedUrl(VideoMediaId), video.GetAttribute("src"));

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"item=media:{VideoMediaId}", nav.Uri);
    }
}

public class Evidence_DeviceTime_Tests : EvidencePageTestBase
{
    [Fact]
    public void ClickDeviceTimeButton_SwitchesToDeviceTimeView_RendersGrid()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        component.Find("[data-testid='view-devicetime']").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=devicetime", nav.Uri);
        Assert.NotEmpty(component.FindAll("[data-testid='device-time-grid']"));
        Assert.Empty(component.FindAll(".evidence-timeline"));
    }

    [Fact]
    public void ClickCell_NarrowsScopeToDeviceAndBucket_AndSwitchesToTimeline()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();

        AngleSharp.Dom.IElement cell = component.Find($"tr[data-key='{DeviceId}'] td");
        cell.Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("view=timeline", nav.Uri);
        _ = Assert.Single(ScopeState.Current.DeviceIds);
        Assert.Equal(DeviceId, ScopeState.Current.DeviceIds[0]);
    }

    [Fact]
    public void ClickDeviceRowHeader_ShowsSnapshotStrip_ForThatDevicesViewableItems()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();

        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='snapshot-strip']"));
        IReadOnlyList<AngleSharp.Dom.IElement> thumbs = component.FindAll("[data-testid='snapshot-strip-thumb']");
        Assert.Equal(2, thumbs.Count); // ImageMedia (via event) + VideoMedia are both viewable
    }

    [Fact]
    public void ClickDeviceRowHeader_FetchesRssiHistory_ForScopeRange()
    {
        _ = DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new() { Id = Guid.NewGuid(), DeviceId = DeviceId, WifiSignalRssi = -60, CapturedAtUtc = EventOccurredAtUtc }
            ]);

        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();
        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        DeviceHealthRepositoryMock.Verify(
            m => m.GetHistoryAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        AngleSharp.Dom.IElement polyline = component.Find("[data-testid='snapshot-strip-rssi'] polyline");
        Assert.False(string.IsNullOrWhiteSpace(polyline.GetAttribute("points")));
    }

    [Fact]
    public void ClickSnapshotStripThumbnail_OpensMediaViewer()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-devicetime']").Click();
        component.Find($"tr[data-key='{DeviceId}'] .device-time-grid-row-header").Click();

        component.Find($"[data-key='event:{EventId}']").Click();

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));
    }
}

/// <summary>
/// The Grid view's context menu (legal hold place/release, export, device integrity verification,
/// view details) moved over from the retired Events page. The Syncfusion grid is stubbed (see
/// EvidencePageTestBase), so selection and context-menu clicks are driven directly through
/// ForensicGrid's public HandleRowSelectedAsync/ContextMenuItemClicked members, the same way
/// ForensicGridTests exercises them.
/// </summary>
public class Evidence_GridContextMenu_Tests : EvidencePageTestBase
{
    /// <summary>
    /// Renders the Evidence page on the Grid view. The item to target is passed via the context
    /// menu event's own RowInfo.RowData (mirroring Syncfusion's real behavior: a right-click
    /// targets the row under the cursor without changing selection), not via a prior row-selection
    /// step - selecting a viewable item would open the media viewer and remove the grid from the
    /// render tree before the context menu could even be considered.
    /// </summary>
    private IRenderedComponent<Evidence> RenderGrid()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/media?view=grid", replace: true);
        return RenderEvidencePage();
    }

    /// <summary>
    /// Builds a ContextMenuClickEventArgs carrying the given row via RowInfo.RowData, the way
    /// Syncfusion's real grid populates it for a right-click on that row (independent of any
    /// left-click "selection" state). RowInfo/RowData's setters are internal to Syncfusion's
    /// assembly, so this uses reflection purely to construct the args - it does not touch
    /// production code.
    /// </summary>
    private static Task ClickContextMenuAsync(IRenderedComponent<Evidence> component, string menuId, EvidenceItem? item)
    {
        return component.InvokeAsync(() =>
        {
            var args = new ContextMenuClickEventArgs<EvidenceItem> { Item = new MenuItemModel { Id = menuId } };
            if (item is not null)
            {
                var rowInfo = new RowInfo<EvidenceItem>();
                typeof(RowInfo<EvidenceItem>).GetProperty(nameof(RowInfo<EvidenceItem>.RowData))!.SetValue(rowInfo, item);
                typeof(ContextMenuClickEventArgs<EvidenceItem>).GetProperty(nameof(ContextMenuClickEventArgs<EvidenceItem>.RowInfo))!.SetValue(args, rowInfo);
            }

            IRenderedComponent<ForensicGrid<EvidenceItem>> grid = component.FindComponent<ForensicGrid<EvidenceItem>>();
            return grid.Instance.ContextMenuItemClicked.InvokeAsync(args);
        });
    }

    private static EvidenceItem FindItem(IRenderedComponent<Evidence> component, string key)
    {
        return component.FindComponent<ForensicGrid<EvidenceItem>>().Instance.DataSource!.First(i => i.Key == key);
    }

    /// <summary>
    /// Syncfusion's grid builds its context menu from ContextMenuItems and expects typed
    /// ContextMenuItemModel entries; anonymous objects crash the grid's ContextMenu during
    /// OnInitialized (NullReferenceException in ContextMenu.GetMenuItems), which takes the Blazor circuit down.
    /// </summary>
    [Fact]
    public void GridContextMenuItems_AreTypedSyncfusionItemModels_WithStableIds()
    {
        IRenderedComponent<Evidence> component = RenderGrid();

        List<object>? items = component.FindComponent<ForensicGrid<EvidenceItem>>().Instance.ContextMenuItems;

        Assert.NotNull(items);
        Assert.All(items, i => Assert.IsType<ContextMenuItemModel>(i));
        Assert.Equal(
            new[] { "details", "export", "hold", "release", "verify" },
            items.Cast<ContextMenuItemModel>().Select(i => i.Id));
    }

    [Fact]
    public async Task Export_SelectedEventWithMedia_NavigatesToReviewExportWithMediaId()
    {
        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");

        await ClickContextMenuAsync(component, "export", item);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"/review/export?ids={ImageMediaId}", nav.Uri);
    }

    [Fact]
    public async Task ContextMenuClicked_NoRowInfo_DoesNothing_DoesNotThrow()
    {
        // Defensive guard: no RowInfo means the click didn't target a row (or the RowInfo shape
        // changed upstream) - nothing should blow up or navigate.
        IRenderedComponent<Evidence> component = RenderGrid();
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        string beforeUri = nav.Uri;

        await ClickContextMenuAsync(component, "export", item: null);

        Assert.Equal(beforeUri, nav.Uri);
    }

    [Fact]
    public async Task PlaceHold_Click_RevealsReasonPrompt()
    {
        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");

        await ClickContextMenuAsync(component, "hold", item);

        Assert.NotEmpty(component.FindAll("[data-testid='grid-action-reason']"));
    }

    [Fact]
    public async Task PlaceHold_ConfirmWithReason_CallsPlaceAsync_WithSignedInOperatorIdentity_ShowsToast()
    {
        Guid operatorId = await SignInOperatorAsync();
        _ = LegalHoldRepositoryMock
            .Setup(r => r.PlaceAsync(ImageMediaId, "Active investigation", operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegalHold { Id = Guid.NewGuid(), MediaItemId = ImageMediaId, Reason = "Active investigation", CreatedBy = "op" });

        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "hold", item);

        component.Find("[data-testid='grid-action-reason']").Change("Active investigation");
        await component.InvokeAsync(() => component.Find("[data-testid='grid-confirm-action']").Click());

        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(ImageMediaId, "Active investigation", operatorId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains("Legal hold placed", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='grid-action-reason']"));
    }

    [Fact]
    public async Task PlaceHold_OperatorSignedOut_ShowsSignInPrompt_NoGridOrPlaceCall()
    {
        await SignOutOperatorAsync();
        IRenderedComponent<Evidence> component = RenderGrid();

        Assert.Contains("Not signed in.", component.Markup);
        Assert.Empty(component.FindComponents<ForensicGrid<EvidenceItem>>());
        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaceHold_SignedOutAfterReasonPrompt_ConfirmRefused_DoesNotCallPlaceAsync_ShowsSignInToast()
    {
        // The fixture starts signed in, so the grid and reason prompt render. The page reads the
        // session only at load and never re-checks it on change, so clearing the session while the
        // prompt is open leaves Confirm reachable: the click runs ConfirmPendingHoldActionAsync's own
        // guard with no operator identity. The grid staying rendered shows the page's gate did not stop it.
        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "hold", item);

        await SignOutOperatorAsync();
        component.Find("[data-testid='grid-action-reason']").Change("Active investigation");
        await component.InvokeAsync(() => component.Find("[data-testid='grid-confirm-action']").Click());

        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains("You must be signed in to perform this action.", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='grid-action-reason']"));
        Assert.NotEmpty(component.FindComponents<ForensicGrid<EvidenceItem>>());
    }

    [Fact]
    public async Task ReleaseHold_ConfirmWithReason_CallsReleaseAsync_WithSignedInOperatorIdentity_ShowsToast()
    {
        Guid operatorId = await SignInOperatorAsync();
        var activeHold = new LegalHold { Id = Guid.NewGuid(), MediaItemId = ImageMediaId, Reason = "r", CreatedBy = "op" };
        _ = LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeHold]);
        _ = LegalHoldRepositoryMock
            .Setup(r => r.ReleaseAsync(activeHold.Id, operatorId.ToString(), "No longer needed", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "release", item);

        component.Find("[data-testid='grid-action-reason']").Change("No longer needed");
        await component.InvokeAsync(() => component.Find("[data-testid='grid-confirm-action']").Click());

        LegalHoldRepositoryMock.Verify(
            r => r.ReleaseAsync(activeHold.Id, operatorId.ToString(), "No longer needed", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains("Legal hold released", component.Markup);
    }

    [Fact]
    public async Task ReleaseHold_OperatorSignedOut_ShowsSignInPrompt_NoGridOrReleaseCall()
    {
        await SignOutOperatorAsync();
        IRenderedComponent<Evidence> component = RenderGrid();

        Assert.Contains("Not signed in.", component.Markup);
        Assert.Empty(component.FindComponents<ForensicGrid<EvidenceItem>>());
        LegalHoldRepositoryMock.Verify(
            r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseHold_SignedOutAfterReasonPrompt_ConfirmRefused_DoesNotCallReleaseAsync_ShowsSignInToast()
    {
        // Same path as PlaceHold_SignedOutAfterReasonPrompt: an active hold makes "release" available,
        // the reason prompt opens while signed in, then the session is cleared before Confirm.
        var activeHold = new LegalHold { Id = Guid.NewGuid(), MediaItemId = ImageMediaId, Reason = "r", CreatedBy = "op" };
        _ = LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeHold]);

        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "release", item);

        await SignOutOperatorAsync();
        component.Find("[data-testid='grid-action-reason']").Change("No longer needed");
        await component.InvokeAsync(() => component.Find("[data-testid='grid-confirm-action']").Click());

        LegalHoldRepositoryMock.Verify(
            r => r.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains("You must be signed in to perform this action.", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='grid-action-reason']"));
        Assert.NotEmpty(component.FindComponents<ForensicGrid<EvidenceItem>>());
    }

    [Fact]
    public async Task CancelPendingHold_HidesReasonPrompt_DoesNotCallRepository()
    {
        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "hold", item);

        component.Find("[data-testid='grid-action-reason']").Change("Some reason");
        await component.InvokeAsync(() => component.Find("[data-testid='grid-cancel-action']").Click());

        Assert.Empty(component.FindAll("[data-testid='grid-action-reason']"));
        LegalHoldRepositoryMock.Verify(
            r => r.PlaceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyIntegrity_CallsServiceScopedToDevice_ShowsToastWithCounts()
    {
        _ = EvidenceValidationServiceMock
            .Setup(s => s.VerifyLocalIntegrityAsync(DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new() { MediaItemId = ImageMediaId, FileName = "motion.jpg", Status = "verified" },
                new() { MediaItemId = VideoMediaId, FileName = "clip.mp4", Status = "failed", FailureReason = "hash mismatch" }
            ]);

        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");
        await ClickContextMenuAsync(component, "verify", item);

        EvidenceValidationServiceMock.Verify(s => s.VerifyLocalIntegrityAsync(DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("1 of 2 file(s) verified for this device.", component.Markup);
    }

    [Fact]
    public async Task ViewDetails_ViewableItem_OpensMediaViewer()
    {
        IRenderedComponent<Evidence> component = RenderGrid();
        EvidenceItem item = FindItem(component, $"event:{EventId}");

        await ClickContextMenuAsync(component, "details", item);

        Assert.NotEmpty(component.FindAll("[data-testid='media-viewer']"));
    }
}

/// <summary>
/// Wiring tests for the media viewer's "grab still with hash" callback: Evidence.razor forwards
/// the selected video's media ID and the viewer's reported frame offset to
/// IMediaStillCaptureService, and reflects the result (hash + open link, or the error) back into
/// the viewer via StillCaptureStatus.
/// </summary>
public class Evidence_GrabStill_Tests : EvidencePageTestBase
{
    private IRenderedComponent<Evidence> OpenVideoViewer()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();
        component.Find("[data-testid='view-gallery']").Click();
        component.Find($".gallery-tile[data-key='event:{EventId}']").Click();
        component.Find("[data-testid='viewer-next']").Click(); // moves to the video item
        return component;
    }

    [Fact]
    public async Task GrabStillButton_Click_CallsServiceWithSelectedMediaIdAndFrameOffset()
    {
        Guid operatorId = await SignInOperatorAsync(); // defaults to "Review"
        _ = MediaStillCaptureServiceMock
            .Setup(s => s.CaptureStillAsync(VideoMediaId, 2500, null, null, null, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStillCaptureResult
            {
                Error = MediaStillCaptureError.None,
                Still = new MediaStillInfo
                {
                    Id = Guid.NewGuid(),
                    SourceMediaItemId = VideoMediaId,
                    FrameOffsetMs = 2500,
                    Sha256Hash = "abc123still",
                    SourceSha256AtCapture = "sourcehash",
                    CapturedByOperator = "operator-1",
                    CaptureMethod = "Server",
                    CreatedAtUtc = DateTime.UtcNow,
                    PinnedToCase = false
                }
            });

        IRenderedComponent<Evidence> component = OpenVideoViewer();
        component.Find("[data-testid='grab-still']").Click();

        MediaStillCaptureServiceMock.Verify(
            s => s.CaptureStillAsync(VideoMediaId, 2500, null, null, null, operatorId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);

        AngleSharp.Dom.IElement result = component.Find("[data-testid='grab-still-result']");
        Assert.Contains("abc123still", result.TextContent);
    }

    [Fact]
    public async Task GrabStillButton_Click_ServiceFails_ShowsErrorMessage()
    {
        Guid operatorId = await SignInOperatorAsync();
        _ = MediaStillCaptureServiceMock
            .Setup(s => s.CaptureStillAsync(VideoMediaId, 2500, null, null, null, operatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediaStillCaptureResult.Fail(MediaStillCaptureError.ExtractionFailed, "ffmpeg not available"));

        IRenderedComponent<Evidence> component = OpenVideoViewer();
        component.Find("[data-testid='grab-still']").Click();

        AngleSharp.Dom.IElement error = component.Find("[data-testid='grab-still-error']");
        Assert.Contains("ffmpeg not available", error.TextContent);
    }

    [Fact]
    public async Task OperatorSignedOut_ViewerNotOpened_ShowsSignInPrompt_NoStillCaptured()
    {
        await SignOutOperatorAsync();
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        Assert.Contains("Not signed in.", component.Markup);
        Assert.Empty(component.FindComponents<MediaViewer>());
        MediaStillCaptureServiceMock.Verify(
            s => s.CaptureStillAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OperatorSignedOutAfterViewerOpened_GrabStillInvoked_DoesNotCallService_ShowsSignInToast()
    {
        // The viewer opens while signed in (the page only gates on session at load), then the session
        // is cleared. Invoke the viewer's bound callback directly, as a click would, so OnGrabStillAsync's
        // own guard runs rather than the page-level sign-in gate.
        IRenderedComponent<Evidence> component = OpenVideoViewer();
        await SignOutOperatorAsync();

        IRenderedComponent<MediaViewer> mediaViewer = component.FindComponent<MediaViewer>();
        await component.InvokeAsync(() => mediaViewer.Instance.OnGrabStill.InvokeAsync(2500));

        MediaStillCaptureServiceMock.Verify(
            s => s.CaptureStillAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Contains("You must be signed in to perform this action.", component.Markup);
    }

    [Fact]
    public async Task BelowReviewRole_GrabStillButtonAbsent_AndHandlerRefuses()
    {
        _ = await SignInOperatorAsync(role: "ReadOnly");

        IRenderedComponent<Evidence> component = OpenVideoViewer();
        Assert.Empty(component.FindAll("[data-testid='grab-still']"));

        IRenderedComponent<MediaViewer> mediaViewer = component.FindComponent<MediaViewer>();
        await component.InvokeAsync(() => mediaViewer.Instance.OnGrabStill.InvokeAsync(2500));

        MediaStillCaptureServiceMock.Verify(
            s => s.CaptureStillAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class Evidence_SignIn_Tests : EvidencePageTestBase
{
    [Fact]
    public async Task Evidence_OperatorSignedInWithNoProviderAccount_ShowsEvidenceNotSignInPrompt()
    {
        // No provider (Ring) account linked, but an operator is signed in: browsing evidence needs
        // only the operator session, so the sign-in prompt must not render.
        _ = await SignInOperatorAsync();
        _ = AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        Assert.DoesNotContain("Not signed in.", component.Markup);
        Assert.NotEmpty(component.FindAll(".timeline-row"));
    }

    [Fact]
    public async Task Evidence_OperatorNotSignedIn_ShowsSignInPrompt()
    {
        // Provider account linked, but no operator session: the page must show the sign-in prompt.
        await SignOutOperatorAsync();
        _ = AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        Assert.Contains("Not signed in.", component.Markup);
        Assert.Contains("to browse evidence.", component.Markup);
        Assert.Empty(component.FindAll(".timeline-row"));
    }
}

public class Evidence_TimelineTone_Tests : EvidencePageTestBase
{
    [Fact]
    public void PinnedEvent_RowLabelHasInCaseTone()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new() { Id = Guid.NewGuid(), CaseNumber = "manual-1", Title = "Case", CreatedBy = "op" }]);
        _ = CaseRepositoryMock
            .Setup(r => r.ListItemsAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new() { Id = Guid.NewGuid(), CaseId = Guid.NewGuid(), Kind = CaseItemKind.Event, EventId = EventId, Reason = "r", AddedBy = "op" }]);

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        AngleSharp.Dom.IElement label = component.Find($".timeline-row[data-key='event:{EventId}'] .timeline-row-label");
        Assert.Contains("timeline-tone-in-case", label.ClassName);
    }

    [Fact]
    public void UnpinnedMotionEvent_RowLabelHasMotionTone()
    {
        IRenderedComponent<Evidence> component = RenderEvidencePage();

        AngleSharp.Dom.IElement label = component.Find($".timeline-row[data-key='event:{EventId}'] .timeline-row-label");
        Assert.Contains("timeline-tone-motion", label.ClassName);
        Assert.Equal(label.TextContent, label.GetAttribute("title"));
        Assert.DoesNotContain("timeline-row-kind", component.Markup);
        _ = Assert.Single(component.FindAll($".timeline-row[data-key='event:{EventId}'] .timeline-row-thumb"));
    }

    [Fact]
    public void CaseLookupThrows_EvidenceStillRendersWithoutInCaseTone()
    {
        _ = CaseRepositoryMock
            .Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("case store unreachable"));

        IRenderedComponent<Evidence> component = RenderEvidencePage();

        AngleSharp.Dom.IElement label = component.Find($".timeline-row[data-key='event:{EventId}'] .timeline-row-label");
        Assert.DoesNotContain("timeline-tone-in-case", label.ClassName);
    }
}
