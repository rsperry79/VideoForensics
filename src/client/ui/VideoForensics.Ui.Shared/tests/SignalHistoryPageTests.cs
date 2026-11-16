using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services.Scope;
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
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void SignalHistory_Renders_TheFlaggedMediaTable()
    {
        IRenderedComponent<SignalHistory> page = RenderPage();

        FlaggedMediaTable table = page.FindComponent<FlaggedMediaTable>().Instance;

        Assert.NotNull(table);
        Assert.Equal(new[] { "RssiDeviation" }, table.Types);
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
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
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

    /// <summary>Renders one camera whose all-time mean is -60 dBm with a 10 dBm sd, and a week grid with the given first cells.</summary>
    private IRenderedComponent<SignalHistory> RenderWithGrid(params (int Index, double Mean)[] boxes)
    {
        _ = RegisterDeviceRepository();
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow.AddDays(1);
        var cells = Enumerable.Range(0, SignalCalendar.Days * SignalCalendar.Hours)
            .Select(_ => SignalStats.Compute(Array.Empty<SignalSample>()))
            .ToList();
        foreach ((int index, double mean) in boxes)
        {
            cells[index] = SignalStats.Compute([new SignalSample(from.AddHours(1), (int)mean)]);
        }

        var calendar = new SignalCalendar(
            cells,
            Enumerable.Range(0, SignalCalendar.Hours).Select(_ => SignalStats.Compute(Array.Empty<SignalSample>())).ToList(),
            Enumerable.Range(0, SignalCalendar.Days).Select(_ => SignalStats.Compute(Array.Empty<SignalSample>())).ToList());
        var allTime = new SignalStats(Count: 10, Min: -95, Max: -40, Average: -60, Latest: -60, PercentWeak: 0, StdDev: 10);
        var device = new SignalDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "ring-front",
            allTime,
            allTime,
            Array.Empty<SignalSample>(),
            Array.Empty<SignalPeriod>(),
            Array.Empty<SignalDeviationWindow>(),
            calendar,
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
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void CalendarBox_IsGreenUnderOneStdDevAndRedAtTwoOrMore()
    {
        // Monday 00:00 is at index 0 and Monday 01:00 at index 1. Mean -65 is 0.5 sd, mean -80 is 2 sd.
        IRenderedComponent<SignalHistory> page = RenderWithGrid((0, -65), (1, -80));

        Assert.Single(page.FindAll("[data-testid='signal-calendar'] tbody tr:first-child td.calendar-box-green"));
        Assert.Single(page.FindAll("[data-testid='signal-calendar'] tbody tr:first-child td.calendar-box-red"));
    }

    [Fact]
    public void CalendarBox_IsOnlyOnFilledBoxes()
    {
        IRenderedComponent<SignalHistory> page = RenderWithGrid((0, -65));

        Assert.Single(page.FindAll("[data-testid='signal-calendar'] tbody td[class*='calendar-box-']"));
    }

    private IStringLocalizer<SignalHistory> PageL => Services.GetRequiredService<IStringLocalizer<SignalHistory>>();

    private const string FirstBoxCell = "[data-testid='signal-calendar'] tbody tr:first-child td:nth-child(2)";
    private const string SecondBoxCell = "[data-testid='signal-calendar'] tbody tr:first-child td:nth-child(3)";
    private const string FooterCells = "[data-testid='signal-calendar'] tfoot";

    /// <summary>A grid box's stats: a mean over the count readings, with an optional sample sd.</summary>
    private static SignalStats Box(int count, double mean, double? stdDev = null)
    {
        return new SignalStats(Count: count, Min: null, Max: null, Average: mean, Latest: null, PercentWeak: 0, StdDev: stdDev);
    }

    /// <summary>A week grid from the given cells, hour-of-day and day-of-week profiles, keyed by index. Missing entries are empty.</summary>
    private static SignalCalendar BuildCalendar(Dictionary<int, SignalStats> cells, Dictionary<int, SignalStats> hours, Dictionary<int, SignalStats> days)
    {
        SignalStats empty = SignalStats.Compute(Array.Empty<SignalSample>());
        return new SignalCalendar(
            Enumerable.Range(0, SignalCalendar.Days * SignalCalendar.Hours).Select(i => cells.GetValueOrDefault(i, empty)).ToList(),
            Enumerable.Range(0, SignalCalendar.Hours).Select(i => hours.GetValueOrDefault(i, empty)).ToList(),
            Enumerable.Range(0, SignalCalendar.Days).Select(i => days.GetValueOrDefault(i, empty)).ToList());
    }

    /// <summary>One camera with a -60 dBm all-time mean and a 10 dBm sd, and the given grid.</summary>
    private static SignalDeviceAnalysis DeviceWithCalendar(SignalCalendar calendar)
    {
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow.AddDays(1);
        var allTime = new SignalStats(Count: 10, Min: -95, Max: -40, Average: -60, Latest: -60, PercentWeak: 0, StdDev: 10);
        return new SignalDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "ring-front",
            allTime,
            allTime,
            Array.Empty<SignalSample>(),
            Array.Empty<SignalPeriod>(),
            Array.Empty<SignalDeviationWindow>(),
            calendar,
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Hour),
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Day),
            SignalBuckets.Compute(Array.Empty<SignalSample>(), from, to, SignalBucket.Week));
    }

    /// <summary>Renders the page. The first load gets the first analysis, and each later load (a scope change) the next one.</summary>
    private IRenderedComponent<SignalHistory> RenderSignal(params SignalDeviceAnalysis[] responses)
    {
        _ = RegisterDeviceRepository();
        var queue = new Queue<SignalAnalysis>(responses.Select(r => new SignalAnalysis([r])));
        var service = new Mock<ISignalAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek());
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0).ToDto());
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void CalendarBox_TooltipShowsItsValue_ReadingCount_AndWeekAndHourSigma()
    {
        IRenderedComponent<SignalHistory> page = RenderSignal(DeviceWithCalendar(BuildCalendar(
            new() { [0] = Box(2, -60, 3.5) },
            new() { [0] = Box(4, -60, 6.8) },
            new() { [0] = Box(5, -60, 4.2) })));

        string title = page.Find(FirstBoxCell).GetAttribute("title");

        Assert.Equal(
            PageL["CalendarCellTitle", PageL["CalendarTipValue"].Value, "-60.0 dBm", 2, PageL["CalendarTipWeekSigma"].Value, "4.2", PageL["CalendarTipHourSigma"].Value, "6.8"].Value,
            title);
    }

    [Fact]
    public void GridMode_ReadingShowsTheMean_AndDeviationShowsDToOneDecimal()
    {
        // The box mean of -75 dBm sits 1.5 sd from the all-time mean of -60 dBm (sd 10). The next box is empty.
        IRenderedComponent<SignalHistory> page = RenderSignal(DeviceWithCalendar(BuildCalendar(new() { [0] = Box(1, -75) }, new(), new())));

        Assert.Equal("-75", page.Find(FirstBoxCell).TextContent.Trim());

        page.Find("[data-testid='signal-grid-mode-deviation']").Click();

        Assert.Equal("1.5", page.Find(FirstBoxCell).TextContent.Trim());
        Assert.Equal(string.Empty, page.Find(SecondBoxCell).TextContent.Trim());

        page.Find("[data-testid='signal-grid-mode-reading']").Click();

        Assert.Equal("-75", page.Find(FirstBoxCell).TextContent.Trim());
    }

    [Fact]
    public void ChangingTheRange_RedrawsTheGrid_ButLeavesTheFooterSigmaUnchanged()
    {
        Dictionary<int, SignalStats> hours = new() { [0] = Box(4, -60, 6.8) };
        SignalDeviceAnalysis before = DeviceWithCalendar(BuildCalendar(new() { [0] = Box(2, -65) }, hours, new()));
        SignalDeviceAnalysis after = DeviceWithCalendar(BuildCalendar(new() { [0] = Box(2, -80) }, hours, new()));
        IRenderedComponent<SignalHistory> page = RenderSignal(before, after);
        string footer = page.Find(FooterCells).TextContent;
        Assert.Equal("-65", page.Find(FirstBoxCell).TextContent.Trim());

        Services.GetRequiredService<ScopeState>().Set(new ForensicScope([], DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1), null));

        page.WaitForAssertion(() => Assert.Equal("-80", page.Find(FirstBoxCell).TextContent.Trim()));
        Assert.Equal(footer, page.Find(FooterCells).TextContent);
    }
}
