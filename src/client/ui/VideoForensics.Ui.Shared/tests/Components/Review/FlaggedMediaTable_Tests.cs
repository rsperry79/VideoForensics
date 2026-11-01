using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using AngleSharp.Dom;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The flagged-media list: loads one page of the requested flag types and shows the rows, count and empty state.</summary>
public class FlaggedMediaTable_Tests : BunitContext
{
    private readonly Mock<IMediaFlagRepository> _repository = new();

    public FlaggedMediaTable_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_repository.Object);
    }

    private static MediaFlag Flag(MediaFlagType type)
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = type,
            DeviceId = Guid.NewGuid(),
            WindowStartUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            WindowEndUtc = new DateTime(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc),
            RaisedAtUtc = new DateTime(2026, 10, 30, 12, 6, 0, DateTimeKind.Utc),
        };
    }

    [Fact]
    public void FlaggedMediaTable_Renders_RowsForRequestedTypes()
    {
        MediaFlagFilter? seen = null;
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .Callback<MediaFlagFilter, int, int, CancellationToken>((filter, _, _, _) => seen = filter)
            .ReturnsAsync(new MediaFlagPage([Flag(MediaFlagType.RssiDeviation), Flag(MediaFlagType.RssiDeviation)], 2));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.RssiDeviation }));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid=flagged-media-table] tbody tr").Count));
        Assert.NotNull(seen);
        Assert.Equal(new[] { MediaFlagType.RssiDeviation }, seen!.Types);
    }

    [Fact]
    public void FlaggedMediaTable_Empty_ShowsLocalizedEmptyMessage()
    {
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage([], 0));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.BitrateCollapse }));

        IElement empty = cut.WaitForElement("[data-testid=flagged-media-empty]");
        Assert.Equal(TestLocalizer.Create()["FlaggedMediaEmpty"].Value, empty.TextContent.Trim());
    }

    [Fact]
    public void FlaggedMediaTable_MoreThanPageSize_ShowsCount()
    {
        var page = Enumerable.Range(0, 200).Select(_ => Flag(MediaFlagType.BitrateCollapse)).ToList();
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(page, 250));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.BitrateCollapse }));

        IElement count = cut.WaitForElement("[data-testid=flagged-media-count]");
        Assert.Equal(TestLocalizer.Create()["FlaggedMediaShowingCount", 200, 250].Value, count.TextContent.Trim());
    }

    [Fact]
    public void FlaggedMediaTable_BitrateFlag_LinksToBitrateReview()
    {
        MediaFlag flag = Flag(MediaFlagType.BitrateCollapse);
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage([flag], 1));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.BitrateCollapse }));

        cut.WaitForAssertion(() => Assert.Equal($"/bitrate-history/review/{flag.Id}",cut.Find("[data-testid=flagged-media-table] tbody tr a").GetAttribute("href")));
    }

    [Fact]
    public void FlaggedMediaTable_RssiFlag_LinksToRssiReview()
    {
        MediaFlag flag = Flag(MediaFlagType.RssiDeviation);
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage([flag], 1));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.RssiDeviation }));

        cut.WaitForAssertion(() => Assert.Equal($"/signal-history/review/{flag.Id}",cut.Find("[data-testid=flagged-media-table] tbody tr a").GetAttribute("href")));
    }

    [Fact]
    public void FlaggedMediaTable_OtherType_HasNoLink()
    {
        _ = _repository
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage([Flag(MediaFlagType.Jamming)], 1));

        IRenderedComponent<FlaggedMediaTable> cut = Render<FlaggedMediaTable>(p => p
            .Add(c => c.Types, new[] { MediaFlagType.Jamming }));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=flagged-media-table] tbody tr")));
        Assert.Empty(cut.FindAll("[data-testid=flagged-media-table] tbody tr a"));
    }
}
