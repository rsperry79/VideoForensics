using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Signal history page shows the server's signal analysis per device and the flagged RSSI deviations.</summary>
public class SignalHistory_Tests : ScopeRailTestContext
{
    private IRenderedComponent<SignalHistory> RenderPage()
    {
        _ = RegisterDeviceRepository();
        var service = new Mock<ISignalAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignalAnalysis(Array.Empty<SignalDeviceAnalysis>()));
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0));
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void SignalHistory_Renders_TheFlaggedMediaTable()
    {
        IRenderedComponent<SignalHistory> page = RenderPage();

        FlaggedMediaTable table = page.FindComponent<FlaggedMediaTable>().Instance;

        Assert.NotNull(table);
        Assert.Equal(new[] { MediaFlagType.RssiDeviation }, table.Types);
    }

    [Fact]
    public void SignalHistory_FlagsHeading_ShowsLocalizedText()
    {
        IRenderedComponent<SignalHistory> page = RenderPage();

        Assert.Equal("Signal deviation flags", page.Find("[data-testid='signal-flags-heading']").TextContent.Trim());
    }

    private IStringLocalizer<SharedResources> SharedL => Services.GetRequiredService<IStringLocalizer<SharedResources>>();

    /// <summary>Two-sigma windows on consecutive past hours, one alert each.</summary>
    private static SignalDeviationWindow[] TwoSigmaHours(int count)
    {
        DateTime latest = DateTime.UtcNow.Date.AddDays(-1);
        return Enumerable.Range(0, count)
            .Select(i => new SignalDeviationWindow(latest.AddHours(-i), SignalDeviationLevel.TwoSigma, 1, -95))
            .ToArray();
    }

    private IRenderedComponent<SignalHistory> RenderWithDeviations(SignalDeviationWindow[] windows)
    {
        _ = RegisterDeviceRepository();
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow.AddDays(1);
        var device = new SignalDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "ring-front",
            SignalStats.Compute(Array.Empty<SignalSample>()),
            SignalStats.Compute(Array.Empty<SignalSample>()),
            Array.Empty<SignalSample>(),
            Array.Empty<SignalPeriod>(),
            windows,
            SignalCalendar.Compute(Array.Empty<SignalSample>(), from, to),
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Hour),
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Day),
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Week));
        var service = new Mock<ISignalAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignalAnalysis([device]));
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0));
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void SevenTwoSigmaAlerts_ShowFiveAndAShowMoreLink()
    {
        IRenderedComponent<SignalHistory> page = RenderWithDeviations(TwoSigmaHours(7));

        Assert.Equal(5, page.FindAll("[data-testid='signal-alert'] li").Count);
        Assert.Equal(SharedL["ShowMoreAlerts", 2].Value, page.Find("[data-testid='signal-alert-toggle']").TextContent.Trim());
    }

    [Fact]
    public void ShowMore_ExpandsTheAlertList_AndShowLessCollapsesIt()
    {
        IRenderedComponent<SignalHistory> page = RenderWithDeviations(TwoSigmaHours(7));

        page.Find("[data-testid='signal-alert-toggle']").Click();

        Assert.Equal(7, page.FindAll("[data-testid='signal-alert'] li").Count);
        Assert.Equal(SharedL["ShowLessAlerts"].Value, page.Find("[data-testid='signal-alert-toggle']").TextContent.Trim());

        page.Find("[data-testid='signal-alert-toggle']").Click();

        Assert.Equal(5, page.FindAll("[data-testid='signal-alert'] li").Count);
    }

    [Fact]
    public void FiveOrFewerTwoSigmaAlerts_ShowNoToggle()
    {
        IRenderedComponent<SignalHistory> page = RenderWithDeviations(TwoSigmaHours(5));

        Assert.Equal(5, page.FindAll("[data-testid='signal-alert'] li").Count);
        Assert.Empty(page.FindAll("[data-testid='signal-alert-toggle']"));
    }
}
