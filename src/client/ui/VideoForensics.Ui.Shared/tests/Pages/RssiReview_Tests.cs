using Bunit;using VideoForensics.Core.Api.Contracts;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests.Pages;

/// <summary>
/// The Signal review page: one RssiDeviation flag and the clip that covers its window inside the shared review shell.
/// A flag that is missing, of another type, has no clip, or whose clips are all purged shows a state message and no player.
/// </summary>
public class RssiReview_Tests : BunitContext
{
    private static readonly DateTime FlagStartUtc = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediaFlagRepository> _flags = new();
    private readonly Mock<IMediaItemRepository> _items = new();
    private readonly Mock<IMediaContentUrlProvider> _urls = new();
    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<ICaseRepository> _cases = new();
    private readonly Mock<ISnapshotCaptureRepository> _snapshots = new();
    private readonly Mock<ILogger<FlagActionsPanel>> _logger = new();
    private readonly Mock<INoteRepository> _notes = new();
    private readonly Mock<ISignalAnalysisService> _signals = new();

    public RssiReview_Tests()
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
        _ = Services.AddSingleton(_devices.Object);
        _ = Services.AddSingleton(_cases.Object);
        _ = Services.AddSingleton(_snapshots.Object);
        _ = Services.AddSingleton(_logger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddLogging();
        _ = Services.AddSingleton(_notes.Object);
        _ = Services.AddSingleton(_signals.Object);
        _ = _signals.Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignalAnalysis([]));
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.MediaFlag, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Note>());
        _ = Services.AddScoped<RightPanelContentService>();

        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _devices.Setup(d => d.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((VideoForensics.Core.Api.Contracts.DeviceDto?)null);
        _ = _snapshots.Setup(s => s.GetCurrentForDeviceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SnapshotCapture>());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MediaItemDto>());
    }

    private IStringLocalizer<SharedResources> Localizer => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    private static MediaFlag Flag(MediaFlagType type)
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = type,
            DeviceId = Guid.NewGuid(),
            MediaItemId = null,
            WindowStartUtc = FlagStartUtc,
            WindowEndUtc = FlagStartUtc.AddMinutes(5),
            RaisedAtUtc = FlagStartUtc.AddMinutes(6),
        };
    }

    private static MediaItem Clip(Guid deviceId, DateTime recordedAtUtc, double durationSeconds, bool purged)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "clip.mp4",
            FilePath = "/clip.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = recordedAtUtc,
            DownloadedAtUtc = recordedAtUtc,
            DurationSeconds = durationSeconds,
            IsPurged = purged,
        };
    }

    private IRenderedComponent<RssiReview> RenderFor(Guid flagId)
    {
        return Render<RssiReview>(p => p.Add(c => c.FlagId, flagId));
    }

    [Fact]
    public void RssiReview_BitrateFlag_ShowsNotFound()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitFlagNotFound"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void RssiReview_NoClipsInWindow_ShowsNoClipInHour()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitNoClipInHour"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void RssiReview_AllClipsPurged_ShowsPurged()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaItem first = Clip(flag.DeviceId, FlagStartUtc, 60, purged: true);
        MediaItem second = Clip(flag.DeviceId, FlagStartUtc.AddMinutes(1), 60, purged: true);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first.ToDto(), second.ToDto() });

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitPurged"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void RssiReview_StillClip_ShowsStillAndNoTimeline()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 60, purged: false);
        clip.FileName = "x.jpg";
        clip.FilePath = "/x.jpg";
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/x.jpg?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=media-fit-still]")));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
        Assert.Empty(cut.FindAll("[data-testid=playback-timeline]"));
    }

    [Fact]
    public void RssiReview_CoveringClip_IsChosen()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        // The first clip starts after the flag window opens, so it does not cover it.
        MediaItem notCovering = Clip(flag.DeviceId, FlagStartUtc.AddMinutes(10), 60, purged: false);
        // The second clip started 30 minutes before the flag window and runs for an hour, so it covers it.
        MediaItem covering = Clip(flag.DeviceId, FlagStartUtc.AddMinutes(-30), 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [covering.Id] = "/clips/covering.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(
                flag.DeviceId,
                FlagStartUtc.AddHours(-1),
                FlagStartUtc.AddMinutes(5).AddHours(1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notCovering.ToDto(), covering.ToDto() });
        _ = _items.Setup(r => r.GetAsync(covering.Id, It.IsAny<CancellationToken>())).ReturnsAsync(covering.ToDto());
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(covering.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        Assert.Equal("/clips/covering.mp4?ticket=test", cut.FindComponent<MediaPlayer>().Instance.Source);
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
    public void RssiReview_FlagWithEventType_PassesEventTypeToTimeline()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        flag.EventType = "person";
        MediaItem covering = Clip(flag.DeviceId, FlagStartUtc.AddMinutes(-30), 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [covering.Id] = "/clips/covering.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { covering.ToDto() });
        _ = _items.Setup(r => r.GetAsync(covering.Id, It.IsAny<CancellationToken>())).ReturnsAsync(covering.ToDto());
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Equal("person", cut.FindComponent<HistogramTimeline>().Instance.EventType));
    }

    [Fact]
    public void RssiReview_NextClick_NavigatesToNeighbourReview()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaFlag neighbour = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, It.IsAny<MediaFlagFilterDto>(), MediaFlagNeighbourDto.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync(neighbour.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        panel.Find("[data-testid=clip-nav-next]").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/signal-history/review/{neighbour.Id}", nav.Uri));
    }

    [Fact]
    public void RssiReview_PreviousClick_NavigatesToNeighbourReview()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaFlag neighbour = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, It.IsAny<MediaFlagFilterDto>(), MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync(neighbour.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        panel.Find("[data-testid=clip-nav-prev]").Click();

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/signal-history/review/{neighbour.Id}", nav.Uri));
    }

    /// <summary>Matches the advance lookup: the page's type filter narrowed to Unreviewed flags.</summary>
    private static MediaFlagFilterDto UnreviewedOfRssi()
    {
        return It.Is<MediaFlagFilterDto>(f => f.Types != null && f.Types.SequenceEqual(new[] { "RssiDeviation" })
            && f.Statuses != null && f.Statuses.SequenceEqual(new[] { "Unreviewed" }));
    }

    /// <summary>Raises the flag actions panel's OnChanged with the flag in the given status, as a Keep or Dismiss does.</summary>
    private async Task RaiseReviewAsync(IRenderedComponent<RssiReview> cut, MediaFlag flag, MediaFlagStatus status)
    {
        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(panelContent);
        IRenderedComponent<ContainerFragment> panel = Render(panelContent);
        FlagActionsPanel actions = panel.FindComponent<FlagActionsPanel>().Instance;
        flag.Status = status;
        await cut.InvokeAsync(() => actions.OnChanged.InvokeAsync(flag.ToDto()));
    }

    [Fact]
    public async Task RssiReview_DismissedFlag_MovesToNextUnreviewedFlag()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaFlag next = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfRssi(), MediaFlagNeighbourDto.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync(next.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Dismissed);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith($"/signal-history/review/{next.Id}", nav.Uri));
    }

    [Fact]
    public async Task RssiReview_NoUnreviewedFlagLeft_StaysAndShowsMessage()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfRssi(), MediaFlagNeighbourDto.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlagDto?)null);
        _ = _flags.Setup(r => r.GetNeighbourAsync(flag.Id, UnreviewedOfRssi(), MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlagDto?)null);
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        string before = Services.GetRequiredService<NavigationManager>().Uri;

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Kept);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=flag-review-no-unreviewed]")));
        Assert.Contains(Localizer["NoUnreviewedFlagsLeft"].Value, cut.Markup);
        Assert.Equal(before, Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task RssiReview_UndoReview_DoesNotMove()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        MediaItem clip = Clip(flag.DeviceId, FlagStartUtc, 3600, purged: false);
        IReadOnlyDictionary<Guid, string> urls = new Dictionary<Guid, string> { [clip.Id] = "/clips/clip.mp4?ticket=test" };
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag.ToDto());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip.ToDto() });
        _ = _urls.Setup(u => u.GetContentUrlsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(urls);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MediaPlayer>()));
        string before = Services.GetRequiredService<NavigationManager>().Uri;

        await RaiseReviewAsync(cut, flag, MediaFlagStatus.Unreviewed);

        Assert.Equal(before, Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(cut.FindAll("[data-testid=flag-review-no-unreviewed]"));
        _flags.Verify(r => r.GetNeighbourAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagFilterDto>(), It.IsAny<MediaFlagNeighbourDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
