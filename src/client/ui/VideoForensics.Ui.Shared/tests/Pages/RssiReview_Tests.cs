using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;
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
        _ = Services.AddScoped<RightPanelContentService>();

        _ = _cases.Setup(c => c.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ForensicCase>());
        _ = _devices.Setup(d => d.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);
        _ = _snapshots.Setup(s => s.GetCurrentForDeviceAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SnapshotCapture>());
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MediaItem>());
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
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);

        IRenderedComponent<RssiReview> cut = RenderFor(flag.Id);

        cut.WaitForAssertion(() => Assert.Contains(Localizer["MediaFitFlagNotFound"].Value, cut.Markup));
        Assert.Empty(cut.FindComponents<MediaPlayer>());
    }

    [Fact]
    public void RssiReview_NoClipsInWindow_ShowsNoClipInHour()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);

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
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

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
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(flag.DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { clip });
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
        _ = _flags.Setup(r => r.GetAsync(flag.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        _ = _items.Setup(r => r.GetByDeviceAndDateRangeAsync(
                flag.DeviceId,
                FlagStartUtc.AddHours(-1),
                FlagStartUtc.AddMinutes(5).AddHours(1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notCovering, covering });
        _ = _items.Setup(r => r.GetAsync(covering.Id, It.IsAny<CancellationToken>())).ReturnsAsync(covering);
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
        Assert.Single(panel.FindAll("[data-testid=flag-actions-panel]"));
        Assert.Single(panel.FindAll("[data-testid=clip-navigator]"));
    }
}
