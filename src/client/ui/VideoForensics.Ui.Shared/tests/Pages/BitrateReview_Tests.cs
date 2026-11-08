using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using System.Text.RegularExpressions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests.Pages;

/// <summary>
/// The Bit rate review page: one BitrateCollapse flag and its clip inside the shared review shell.
/// A flag that is missing, of another type, or whose clip is purged shows a state message and no player.
/// </summary>
public class BitrateReview_Tests : BunitContext
{
    private static readonly DateTime FlagStartUtc = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediaFlagRepository> _flags = new();
    private readonly Mock<IMediaItemRepository> _items = new();
    private readonly Mock<IMediaContentUrlProvider> _urls = new();
    private readonly Mock<IMediaBitrateSeriesSource> _series = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<ISnapshotCaptureRepository> _snapshots = new();
    private readonly Mock<ILogger<FlagActionsPanel>> _logger = new();
    private readonly Mock<INoteRepository> _notes = new();

    public BitrateReview_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton(_flags.Object);
        _ = Services.AddSingleton(_items.Object);
        _ = Services.AddSingleton(_urls.Object);
        _ = Services.AddSingleton(_series.Object);
        _ = Services.AddSingleton(_devices.Object);
        _ = Services.AddSingleton(_cases.Object);
        _ = Services.AddSingleton(_snapshots.Object);
        _ = Services.AddSingleton(_logger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddLogging();
        _ = Services.AddSingleton(_notes.Object);
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.MediaFlag, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Note>());
        _ = Services.AddScoped<RightPanelContentService>();

        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _devices.Setup(d => d.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);
    }

    private IStringLocalizer<SharedResources> Localizer => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    private static MediaFlag Flag(MediaFlagType type, Guid? mediaItemId)
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = type,
            DeviceId = Guid.NewGuid(),
            MediaItemId = mediaItemId,
            WindowStartUtc = FlagStartUtc,
            WindowEndUtc = FlagStartUtc.AddMinutes(5),
            RaisedAtUtc = FlagStartUtc.AddMinutes(6),
        };
    }

    private static MediaItem Clip(Guid deviceId, bool purged)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "clip.mp4",
            FilePath = "/clip.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = FlagStartUtc,
            DownloadedAtUtc = FlagStartUtc,
            DurationSeconds = 60,
            IsPurged = purged,
        };
    }

    [Fact]
    public void BitrateReview_MissingFlag_ShowsOneMessage()
    {
        var flagId = Guid.NewGuid();
        _ = _flags.Setup(r => r.GetAsync(flagId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaFlag?)null);

        IRenderedComponent<BitrateReview> cut = RenderFor(flagId);

        string message = Localizer["MediaFitFlagNotFound"].Value;
        cut.WaitForAssertion(() => Assert.Equal(1, Regex.Matches(cut.Markup, Regex.Escape(message)).Count));
        Assert.Empty(cut.FindAll("[data-testid=bitrate-review-not-found]"));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    private IRenderedComponent<BitrateReview> RenderFor(Guid flagId)
    {
        return Render<BitrateReview>(p => p.Add(c => c.FlagId, flagId));
    }

    [Fact]
    public void BitrateReview_UnknownFlag_ShowsNotFound()
    {
        var flagId = Guid.NewGuid();
        _ = _flags.Setup(r => r.GetAsync(flagId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaFlag?)null);

        IRenderedComponent<BitrateReview> cut = RenderFor(flagId);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitFlagNotFound"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void BitrateReview_RssiFlag_ShowsNotFound()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation, mediaItemId: null);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitFlagNotFound"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void BitrateReview_PurgedClip_ShowsPurgedState()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaItem clip = Clip(flag.DeviceId, purged: true);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitPurged"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void BitrateReview_PlayableClip_RendersShellRegions()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        Assert.NotNull(cut.Find("[data-testid=flag-review-media]"));
        Assert.NotNull(cut.Find("[data-testid=flag-review-timeline]"));
        Assert.Empty(cut.FindAll("[data-testid=flag-review-side]"));
        Assert.Empty(cut.FindAll("[data-testid=flag-actions-panel]"));
        Assert.Empty(cut.FindAll("[data-testid=clip-navigator]"));

        // The flag actions and the clip navigator go to the right panel, which the page hands to RightPanelContentService.
        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        _ = Assert.Single(panel.FindAll("[data-testid=flag-actions-panel]"));
        _ = Assert.Single(panel.FindAll("[data-testid=clip-navigator]"));
    }

    [Fact]
    public void BitrateReview_StillClip_ShowsStillAndNoTimeline()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        clip.FileName = "x.jpg";
        clip.FilePath = "/x.jpg";
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/x.jpg?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=media-fit-still]")));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
        Assert.Empty(cut.FindAll("[data-testid=playback-timeline]"));
    }

    [Fact]
    public void BitrateReview_NextClick_NavigatesToNeighbourReview()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaFlag neighbour = Flag(MediaFlagType.BitrateCollapse, mediaItemId: null);
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, It.IsAny<MediaFlagFilter>(), MediaFlagNeighbour.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync(neighbour);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        panel.Find("[data-testid=clip-nav-next]").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/bitrate-history/review/{neighbour.Id}", nav.Uri));
    }

    [Fact]
    public void BitrateReview_PreviousClick_NavigatesToNeighbourReview()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaFlag neighbour = Flag(MediaFlagType.BitrateCollapse, mediaItemId: null);
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, It.IsAny<MediaFlagFilter>(), MediaFlagNeighbour.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync(neighbour);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        panel.Find("[data-testid=clip-nav-prev]").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/bitrate-history/review/{neighbour.Id}", nav.Uri));
    }

    /// <summary>Matches the advance lookup: the page's type filter narrowed to Unreviewed flags.</summary>
    private static MediaFlagFilter UnreviewedOfBitrate()
    {
        return It.Is<MediaFlagFilter>(f => f.Types != null && f.Types.SequenceEqual(new[] { MediaFlagType.BitrateCollapse })
            && f.Statuses != null && f.Statuses.SequenceEqual(new[] { MediaFlagStatus.Unreviewed }));
    }

    /// <summary>Raises the flag actions panel's OnChanged with the flag in the given status, as a Keep or Dismiss does.</summary>
    private async Task RaiseReviewAsync(IRenderedComponent<BitrateReview> cut, MediaFlag flag, MediaFlagStatus status)
    {
        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        FlagActionsPanel actions = panel.FindComponent<FlagActionsPanel>().Instance;
        flag.Status = status;
        await cut.InvokeAsync(() => actions.OnChanged.InvokeAsync(flag));
    }

    [Fact]
    public async Task BitrateReview_DismissedFlag_MovesToNextUnreviewedFlag()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaFlag next = Flag(MediaFlagType.BitrateCollapse, mediaItemId: null);
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfBitrate(), MediaFlagNeighbour.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Dismissed);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/bitrate-history/review/{next.Id}", nav.Uri));
    }

    [Fact]
    public async Task BitrateReview_NoUnreviewedFlagLeft_StaysAndShowsMessage()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfBitrate(), MediaFlagNeighbour.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlag?)null);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfBitrate(), MediaFlagNeighbour.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlag?)null);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        string before = Services.GetRequiredService<NavigationManager>().Uri;

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Kept);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=flag-review-no-unreviewed]")));
        Assert.Contains(Localizer["NoUnreviewedFlagsLeft"].Value, cut.Markup);
        Assert.Equal(before, Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task BitrateReview_UndoReview_DoesNotMove()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse, Guid.NewGuid());
        MediaItem clip = Clip(flag.DeviceId, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetAsync(flag.MediaItemId!.Value, It.IsAny<CancellationToken>())).ReturnsAsync(clip);
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<BitrateReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        string before = Services.GetRequiredService<NavigationManager>().Uri;

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Unreviewed);

        Assert.Equal(before, Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(cut.FindAll("[data-testid=flag-review-no-unreviewed]"));
        _flags.Verify(r => r.GetNeighbourAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagFilter>(), It.IsAny<MediaFlagNeighbour>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
