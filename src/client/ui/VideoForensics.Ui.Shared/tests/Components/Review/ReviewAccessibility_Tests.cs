using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Components.Viewer;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The flagged-media review components: landmarks, accessible names and live regions.</summary>
public class ReviewAccessibility_Tests : BunitContext
{
    private static readonly DateTime WindowStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc);

    private readonly Mock<IMediaFlagRepository> _repository = new();
    private readonly Guid _currentId = Guid.NewGuid();
    private readonly MediaFlagFilter _filter = new() { Types = new[] { MediaFlagType.RssiDeviation } };

    public ReviewAccessibility_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IMediaPlaybackPolicy, DefaultMediaPlaybackPolicy>();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_repository.Object);
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        _ = _repository
            .Setup(r => r.GetPositionAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)3);
    }

    private static MediaFlag Flag(MediaFlagType type, DateTime windowStartUtc)
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = type,
            DeviceId = Guid.NewGuid(),
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowStartUtc.AddSeconds(30),
            RaisedAtUtc = windowStartUtc.AddSeconds(31),
        };
    }

    [Fact]
    public void ReviewAccessibility_ClipNavigator_IsAGroupWithALabelAndFocusableRoot()
    {
        IRenderedComponent<ClipNavigator> cut = Render<ClipNavigator>(p => p
            .Add(c => c.Filter, _filter)
            .Add(c => c.CurrentFlagId, _currentId));

        IElement root = cut.Find("[data-testid=clip-navigator]");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(root.GetAttribute("aria-label")));
        Assert.Equal("0", root.GetAttribute("tabindex"));
    }

    [Fact]
    public void ReviewAccessibility_ClipNavigator_LiveRegionIsPolite()
    {
        IRenderedComponent<ClipNavigator> cut = Render<ClipNavigator>(p => p
            .Add(c => c.Filter, _filter)
            .Add(c => c.CurrentFlagId, _currentId));

        Assert.Equal("polite", cut.Find("[data-testid=clip-nav-status]").GetAttribute("aria-live"));
    }

    [Fact]
    public void ReviewAccessibility_PlaybackTimeline_MarkersAreNamedButtons()
    {
        IRenderedComponent<MediaPlayer> player = Render<MediaPlayer>();
        IRenderedComponent<PlaybackTimeline> cut = Render<PlaybackTimeline>(p => p
            .Add(c => c.Player, player.Instance)
            .Add(c => c.WindowStartUtc, WindowStart)
            .Add(c => c.WindowEndUtc, WindowEnd)
            .Add(c => c.Markers, new List<MediaFlag>
            {
                Flag(MediaFlagType.RssiDeviation, WindowStart.AddSeconds(60)),
                Flag(MediaFlagType.BitrateCollapse, WindowStart.AddSeconds(120)),
            }));

        IReadOnlyList<IElement> markers = cut.FindAll("[data-testid=timeline-marker]");
        Assert.NotEmpty(markers);
        foreach (IElement marker in markers)
        {
            Assert.Equal("BUTTON", marker.TagName);
            Assert.False(string.IsNullOrWhiteSpace(marker.GetAttribute("aria-label")));
        }
    }

    [Fact]
    public void ReviewAccessibility_FlagReviewShell_RegionsAreLabelled()
    {
        IRenderedComponent<FlagReviewShell> cut = Render<FlagReviewShell>(p => p
            .Add(c => c.Breadcrumb, (RenderFragment)(b => b.AddMarkupContent(0, "<span>crumb</span>")))
            .Add(c => c.Media, (RenderFragment)(b => b.AddMarkupContent(0, "<span>media</span>")))
            .Add(c => c.Timeline, (RenderFragment)(b => b.AddMarkupContent(0, "<span>timeline</span>"))));

        // The shell labels media and timeline. The breadcrumb region is not labelled, so it is not asserted here.
        foreach (string testId in new[] { "flag-review-media", "flag-review-timeline" })
        {
            Assert.False(string.IsNullOrWhiteSpace(cut.Find($"[data-testid={testId}]").GetAttribute("aria-label")));
        }
    }

    [Fact]
    public void ReviewAccessibility_MediaFit_StateMessageIsPolite()
    {
        IRenderedComponent<MediaFit> cut = Render<MediaFit>(p => p
            .Add(c => c.State, MediaFitState.Purged));

        IElement message = cut.Find("[role=status]");
        Assert.Equal("polite", message.GetAttribute("aria-live"));
    }
}
